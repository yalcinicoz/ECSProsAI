using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Domain.Entities;
using ECSPros.Fulfillment.Domain.Events;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.DeskFinalCheckScan;

/// <summary>
/// OP4: son kontrol — "Paketle" denen siparişin raftaki ürünleri masada tek tek okutulur.
/// Yanlış ürün → hata (son ayrıştırma hatası, askıya; aidiyeti koli kapanınca kesinleşir).
/// Tüm ürünler doğrulanınca paket oluşturulur (packed), slot boşaltılır; fatura+yazdırma
/// API katmanında orkestre edilir (fast-lane kalıbı). Stok burada DÜŞMEZ (toplamada düştü).
/// </summary>
public record DeskFinalCheckScanCommand(Guid DeskId, Guid OrderId, string Barcode, Guid ActorId)
    : IRequest<Result<FinalCheckResultDto>>;

public record FinalCheckResultDto(
    int Kalan,               // siparişte son kontrolü bekleyen ürün
    bool Tamam,              // true → paket oluştu
    Guid? PackageId,
    string? PackageNumber,
    Guid? FirmPlatformId,
    string? OrderNumber);

public class DeskFinalCheckScanCommandHandler(
    IFulfillmentDbContext db,
    IPackageNumberService packageNumbers,
    IOrderPackagingReader orderReader,
    IPublisher publisher)
    : IRequestHandler<DeskFinalCheckScanCommand, Result<FinalCheckResultDto>>
{
    public async Task<Result<FinalCheckResultDto>> Handle(DeskFinalCheckScanCommand request, CancellationToken ct)
    {
        var barkod = request.Barcode.Trim();
        if (barkod.Length == 0) return Result.Failure<FinalCheckResultDto>("Barkod boş.");

        var masa = await db.PackingDesks.FirstOrDefaultAsync(d => d.Id == request.DeskId && d.Status == "open", ct);
        if (masa is null) return Result.Failure<FinalCheckResultDto>("Açık masa bulunamadı.");

        var satirlar = await db.PickingPlanLines
            .Where(l => l.PickingPlanId == masa.PickingPlanId && l.OrderId == request.OrderId)
            .ToListAsync(ct);
        if (satirlar.Count == 0) return Result.Failure<FinalCheckResultDto>("Sipariş satırları bulunamadı.");

        var satir = satirlar
            .Where(l => l.VariantBarcode == barkod && l.FinalScannedQuantity < l.Quantity)
            .OrderBy(l => l.RouteOrder)
            .FirstOrDefault();
        if (satir is null)
            return Result.Failure<FinalCheckResultDto>(
                "Bu ürün bu siparişe ait değil — son ayrıştırma hatası, ürünü askıya ayırın.");

        var now = DateTime.UtcNow;
        satir.FinalScannedQuantity += 1;
        satir.UpdatedAt = now;
        satir.UpdatedBy = request.ActorId;
        db.OperationLogs.Add(new OperationLog
        {
            OrderId = request.OrderId, OrderItemId = satir.OrderItemId,
            PickingPlanId = masa.PickingPlanId, Action = "final_scanned",
            ActorId = request.ActorId, CreatedBy = request.ActorId,
            Detail = new Dictionary<string, object>
                { ["desk"] = masa.DeskNumber, ["sku"] = satir.Sku, ["scanned"] = satir.FinalScannedQuantity }
        });

        var kalan = satirlar.Sum(l => l.Quantity - l.FinalScannedQuantity);
        if (kalan > 0)
        {
            await db.SaveChangesAsync(ct);
            await publisher.Publish(new DeskLineProgressEvent(request.OrderId, satir.OrderItemId,
                request.ActorId, 0, 1, masa.DeskNumber.ToString(), null), ct);
            return Result.Success(new FinalCheckResultDto(kalan, false, null, null, null, satir.OrderNumber));
        }

        // Tüm ürünler doğrulandı → paket (tüm kalemler), slot boşalt
        var siparis = await orderReader.GetOrderAsync(request.OrderId, ct);
        if (siparis is null) return Result.Failure<FinalCheckResultDto>("Sipariş okunamadı.");
        var paketNo = await packageNumbers.GenerateAsync(siparis.FirmPlatformId, ct);
        var paket = new Package
        {
            OrderId = request.OrderId,
            FirmPlatformId = siparis.FirmPlatformId,
            PackageNumber = paketNo,
            SequenceInOrder = 1,
            Status = "packed",
            PackedAt = now,
            PackedBy = request.ActorId,
            CreatedBy = request.ActorId
        };
        foreach (var l in satirlar.Where(l => l.Quantity > 0))
            paket.Items.Add(new PackageItem
            {
                PackageId = paket.Id, OrderItemId = l.OrderItemId,
                VariantId = l.VariantId, Quantity = l.Quantity, CreatedBy = request.ActorId
            });
        db.Packages.Add(paket);

        var bin = await db.SortingBins.FirstOrDefaultAsync(
            sb => sb.SortingBoxId == masa.SortingBoxId && sb.OrderId == request.OrderId, ct);
        var slot = bin?.DeskSlotNumber;
        if (bin is not null) { bin.DeskSlotNumber = null; bin.Status = "ready"; }

        db.OperationLogs.Add(new OperationLog
        {
            OrderId = request.OrderId, PickingPlanId = masa.PickingPlanId, PackageId = paket.Id,
            Action = "package_packed", ActorId = request.ActorId, CreatedBy = request.ActorId,
            Detail = new Dictionary<string, object>
                { ["packageNumber"] = paketNo, ["desk"] = masa.DeskNumber, ["slot"] = slot ?? 0 }
        });
        await db.SaveChangesAsync(ct);
        await publisher.Publish(new DeskLineProgressEvent(request.OrderId, satir.OrderItemId,
            request.ActorId, 0, 1, masa.DeskNumber.ToString(), null), ct);

        return Result.Success(new FinalCheckResultDto(0, true, paket.Id, paketNo,
            siparis.FirmPlatformId, satir.OrderNumber));
    }
}
