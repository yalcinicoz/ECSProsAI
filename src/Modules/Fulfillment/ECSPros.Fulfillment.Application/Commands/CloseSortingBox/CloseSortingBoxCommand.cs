using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.CloseSortingBox;

/// <summary>
/// OP4: koli + masa kapanışı — paketlenmemiş siparişler OBM'ye transfer edilir (K-6),
/// koli/masa numaraları yeniden kullanılabilir, personel boşa çıkar. Sistem kontrolü:
/// koliye hâlâ ürün gelebilecekse (ayrıştırılmamış toplanmış ürün varsa) Force olmadan
/// kapatılmaz (kurgu: "o masaya başkaca ürün gelmeyeceğinden eminsek — sistem kontrol eder").
/// </summary>
public record CloseSortingBoxCommand(Guid BoxId, Guid ActorId, bool Force = false)
    : IRequest<Result<CloseBoxDto>>;

public record CloseBoxDto(int PaketlenenSiparis, int ObmSiparis);

public class CloseSortingBoxCommandHandler(IFulfillmentDbContext db)
    : IRequestHandler<CloseSortingBoxCommand, Result<CloseBoxDto>>
{
    public async Task<Result<CloseBoxDto>> Handle(CloseSortingBoxCommand request, CancellationToken ct)
    {
        var koli = await db.SortingBoxes.FirstOrDefaultAsync(b => b.Id == request.BoxId, ct);
        if (koli is null) return Result.Failure<CloseBoxDto>("Koli bulunamadı.");
        if (koli.Status == "closed") return Result.Failure<CloseBoxDto>("Koli zaten kapalı.");

        var binler = await db.SortingBins
            .Where(sb => sb.SortingBoxId == koli.Id && sb.OrderId != null)
            .ToListAsync(ct);
        var orderIdler = binler.Select(b => b.OrderId!.Value).ToList();
        var satirlar = await db.PickingPlanLines
            .Where(l => l.PickingPlanId == koli.PickingPlanId && orderIdler.Contains(l.OrderId))
            .ToListAsync(ct);

        // Sistem kontrolü: kolideki siparişlere hâlâ ürün gelebilir mi
        // (toplanmış ama ayrıştırılmamış ürünü olan sipariş — kurgu gereği kapatma engeli)
        var gelebilecek = satirlar
            .Where(l => l.Status != "short" && l.Status != "returned"
                        && l.SortedQuantity < l.Quantity && l.PickedQuantity > l.SortedQuantity)
            .Select(l => l.OrderId).Distinct().Count();
        if (gelebilecek > 0 && !request.Force)
            return Result.Failure<CloseBoxDto>(
                $"{gelebilecek} siparişin toplanmış ama henüz ayrıştırılmamış ürünü var — koliye ürün gelebilir. Yine de kapatmak için onaylayın.");

        var now = DateTime.UtcNow;
        int paketlenen = 0, obm = 0;
        foreach (var orderId in orderIdler)
        {
            var ls = satirlar.Where(l => l.OrderId == orderId).ToList();
            var tamam = ls.Count > 0 && ls.All(l => l.FinalScannedQuantity >= l.Quantity);
            if (tamam) { paketlenen++; continue; }

            var bin = binler.First(b => b.OrderId == orderId);
            if (!bin.ObmTransferred)
            {
                bin.ObmTransferred = true;
                bin.DeskSlotNumber = null;
                obm++;
                db.OperationLogs.Add(new OperationLog
                {
                    OrderId = orderId, PickingPlanId = koli.PickingPlanId, Action = "obm_transferred",
                    ActorId = request.ActorId, CreatedBy = request.ActorId,
                    Detail = new Dictionary<string, object>
                        { ["box"] = koli.BoxNumber, ["desk"] = koli.StationNumber ?? 0 }
                });
            }
        }

        koli.Status = "closed";
        koli.ClosedAt = now;
        koli.UpdatedAt = now;
        koli.UpdatedBy = request.ActorId;
        if (koli.StationId is { } masaId)
        {
            var masa = await db.PackingDesks.FirstOrDefaultAsync(d => d.Id == masaId, ct);
            if (masa is not null) { masa.Status = "closed"; masa.ClosedAt = now; }
        }
        await db.SaveChangesAsync(ct);
        return Result.Success(new CloseBoxDto(paketlenen, obm));
    }
}
