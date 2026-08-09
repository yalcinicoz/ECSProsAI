using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Domain.Entities;
using ECSPros.Fulfillment.Domain.Events;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.DeskSortScan;

/// <summary>
/// OP4: masa son ayrıştırma okutması — kolideki siparişlerden seçim: ÖNCE en az 1 ürünü
/// slotta olan (kurgu 1. koşul), yoksa yeni siparişe EN KÜÇÜK boş slot; eşitlikte en eski.
/// Slot numarası seslendirilir. Sipariş tamamlanırsa "Paketle" döner (panel sesi + son
/// kontrol modu). Kolide bu ürüne ihtiyaç yoksa hata → ara ayrıştırma hatası, askıya.
/// </summary>
public record DeskSortScanCommand(Guid DeskId, string Barcode, Guid ActorId)
    : IRequest<Result<DeskSortScanResultDto>>;

public record DeskSortScanResultDto(
    int SlotNumber,          // seslendirilecek
    string OrderNumber,
    Guid OrderId,
    int SiparisKalan,        // slota girmesi gereken kalan ürün
    bool Paketle);           // true → "Paketle" + slot seslendirilir, son kontrol başlar

public class DeskSortScanCommandHandler(IFulfillmentDbContext db, IPublisher publisher)
    : IRequestHandler<DeskSortScanCommand, Result<DeskSortScanResultDto>>
{
    public async Task<Result<DeskSortScanResultDto>> Handle(DeskSortScanCommand request, CancellationToken ct)
    {
        var barkod = request.Barcode.Trim();
        if (barkod.Length == 0) return Result.Failure<DeskSortScanResultDto>("Barkod boş.");

        var masa = await db.PackingDesks.FirstOrDefaultAsync(d => d.Id == request.DeskId && d.Status == "open", ct);
        if (masa is null) return Result.Failure<DeskSortScanResultDto>("Açık masa bulunamadı.");

        var binler = await db.SortingBins
            .Where(sb => sb.SortingBoxId == masa.SortingBoxId && sb.OrderId != null && !sb.ObmTransferred)
            .ToListAsync(ct);
        var orderIdler = binler.Select(b => b.OrderId!.Value).ToList();
        var satirlar = await db.PickingPlanLines
            .Where(l => l.PickingPlanId == masa.PickingPlanId && orderIdler.Contains(l.OrderId))
            .ToListAsync(ct);

        // Bu barkoda ihtiyacı olan (slota eksik) sipariş adayları
        var adaylar = satirlar
            .Where(l => l.VariantBarcode == barkod && l.FinalSortedQuantity < l.Quantity)
            .GroupBy(l => l.OrderId)
            .Select(g => new
            {
                OrderId = g.Key,
                OrderNumber = g.First().OrderNumber,
                OrderCreatedAt = g.First().OrderCreatedAt,
                Bin = binler.First(b => b.OrderId == g.Key),
            })
            .OrderByDescending(a => a.Bin.DeskSlotNumber != null) // 1. koşul: slotu olan önce
            .ThenBy(a => a.OrderCreatedAt)                        // eşitlikte en eski
            .ToList();
        if (adaylar.Count == 0)
            return Result.Failure<DeskSortScanResultDto>(
                "Bu kolide bu ürüne ihtiyaç yok — ara ayrıştırma hatası, ürünü askıya ayırın.");

        var secilen = adaylar[0];
        var bin = secilen.Bin;

        // Slot ataması gerekiyorsa: en küçük boş slot (profil StationSlotCount sınırı)
        if (bin.DeskSlotNumber is null)
        {
            var profil = await db.OperationProfiles.AsNoTracking().FirstOrDefaultAsync(ct)
                         ?? new OperationProfile();
            var dolu = binler.Where(b => b.DeskSlotNumber != null)
                .Select(b => b.DeskSlotNumber!.Value).ToHashSet();
            var slot = 1;
            while (dolu.Contains(slot)) slot++;
            if (slot > profil.StationSlotCount)
                return Result.Failure<DeskSortScanResultDto>(
                    $"Masada boş slot kalmadı ({profil.StationSlotCount}) — önce bir sipariş paketleyin.");
            bin.DeskSlotNumber = slot;
        }

        var satir = satirlar
            .Where(l => l.OrderId == secilen.OrderId && l.VariantBarcode == barkod
                        && l.FinalSortedQuantity < l.Quantity)
            .OrderBy(l => l.RouteOrder)
            .First();
        var now = DateTime.UtcNow;
        satir.FinalSortedQuantity += 1;
        satir.UpdatedAt = now;
        satir.UpdatedBy = request.ActorId;
        masa.UpdatedAt = now;

        db.OperationLogs.Add(new OperationLog
        {
            OrderId = secilen.OrderId, OrderItemId = satir.OrderItemId,
            PickingPlanId = masa.PickingPlanId, Action = "slot_assigned",
            ActorId = request.ActorId, CreatedBy = request.ActorId,
            Detail = new Dictionary<string, object>
            {
                ["desk"] = masa.DeskNumber, ["slot"] = bin.DeskSlotNumber!,
                ["sku"] = satir.Sku, ["finalSorted"] = satir.FinalSortedQuantity
            }
        });
        await db.SaveChangesAsync(ct);

        await publisher.Publish(new DeskLineProgressEvent(secilen.OrderId, satir.OrderItemId,
            request.ActorId, 1, 0, masa.DeskNumber.ToString(), bin.DeskSlotNumber), ct);

        var siparisSatirlari = satirlar.Where(l => l.OrderId == secilen.OrderId).ToList();
        var kalan = siparisSatirlari.Sum(l => l.Quantity - l.FinalSortedQuantity);
        return Result.Success(new DeskSortScanResultDto(
            bin.DeskSlotNumber!.Value, secilen.OrderNumber, secilen.OrderId, kalan, kalan == 0));
    }
}
