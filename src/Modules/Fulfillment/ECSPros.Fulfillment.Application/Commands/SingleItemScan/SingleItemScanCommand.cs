using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Domain.Entities;
using ECSPros.Fulfillment.Domain.Events;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.SingleItemScan;

/// <summary>
/// OP2: tek ürünlü hızlı hat — yığından okutulan barkod, görevdeki EN ESKİ onaylı siparişe
/// verilir (K-7); toplama + son kontrol tek adımdır, paket oluşturulur (packed). Fatura
/// kesimi API katmanında orkestre edilir (Order modülü komutu). Eşleşme yoksa hata sesi +
/// ürün depo iadesine ayrılır (K: yanlış ürün yığına atılmış).
/// </summary>
public record SingleItemScanCommand(
    Guid PlanId,
    string Barcode,
    Guid ActorId) : IRequest<Result<SingleItemScanResultDto>>;

public record SingleItemScanResultDto(
    Guid OrderId, string OrderNumber, Guid PackageId, string PackageNumber, Guid FirmPlatformId);

public class SingleItemScanCommandHandler(
    IFulfillmentDbContext db,
    IPackageNumberService packageNumbers,
    IOrderPackagingReader orderReader,
    IPublisher publisher)
    : IRequestHandler<SingleItemScanCommand, Result<SingleItemScanResultDto>>
{
    public async Task<Result<SingleItemScanResultDto>> Handle(SingleItemScanCommand request, CancellationToken ct)
    {
        var barkod = request.Barcode.Trim();
        if (barkod.Length == 0) return Result.Failure<SingleItemScanResultDto>("Barkod boş.");

        // K-7: en eski onaylı sipariş — barkodu eşleşen, tamamlanmamış satırlar arasından
        var satir = await db.PickingPlanLines
            .Where(l => l.PickingPlanId == request.PlanId
                        && l.VariantBarcode == barkod
                        && l.PickedQuantity < l.Quantity
                        && (l.Status == "pending" || l.Status == "assigned"))
            .OrderBy(l => l.OrderCreatedAt)
            .FirstOrDefaultAsync(ct);
        if (satir is null)
            return Result.Failure<SingleItemScanResultDto>(
                "Bu ürüne bu görevdeki hiçbir siparişin ihtiyacı yok — depo iadesine ayırın.");

        var siparis = await orderReader.GetOrderAsync(satir.OrderId, ct);
        if (siparis is null) return Result.Failure<SingleItemScanResultDto>("Sipariş okunamadı.");

        var now = DateTime.UtcNow;
        // Toplama + son kontrol tek adım (tek ürünlü hatta ayrıştırma yok)
        satir.PickedQuantity = satir.Quantity;
        satir.PickedBinId = satir.SourceBinId;
        satir.PickedBinCode = satir.SourceBinCode;
        satir.PickedBy = request.ActorId;
        satir.PickedAt = now;
        satir.Status = "picked";
        satir.UpdatedAt = now;
        satir.UpdatedBy = request.ActorId;

        // Paket: tek kalem, doğrudan packed
        var paketNo = await packageNumbers.GenerateAsync(siparis.FirmPlatformId, ct);
        var paket = new Package
        {
            OrderId = satir.OrderId,
            FirmPlatformId = siparis.FirmPlatformId,
            PackageNumber = paketNo,
            SequenceInOrder = 1,
            Status = "packed",
            PackedAt = now,
            PackedBy = request.ActorId,
            CreatedBy = request.ActorId
        };
        paket.Items.Add(new PackageItem
        {
            PackageId = paket.Id,
            OrderItemId = satir.OrderItemId,
            VariantId = satir.VariantId,
            Quantity = satir.Quantity,
            CreatedBy = request.ActorId
        });
        db.Packages.Add(paket);

        db.OperationLogs.Add(new OperationLog
        {
            OrderId = satir.OrderId, OrderItemId = satir.OrderItemId,
            PickingPlanId = request.PlanId, PackageId = paket.Id, Action = "line_picked",
            ActorId = request.ActorId, CreatedBy = request.ActorId,
            Detail = new Dictionary<string, object> { ["sku"] = satir.Sku, ["fastLane"] = true }
        });
        db.OperationLogs.Add(new OperationLog
        {
            OrderId = satir.OrderId, PickingPlanId = request.PlanId, PackageId = paket.Id,
            Action = "package_packed", ActorId = request.ActorId, CreatedBy = request.ActorId,
            Detail = new Dictionary<string, object> { ["packageNumber"] = paketNo }
        });
        await db.SaveChangesAsync(ct);

        // Son kontrol de yapılmış sayılır (FinalScanned=true) — OrderItem + stok senkronu
        await publisher.Publish(new PickingLinePickedEvent(request.PlanId, request.ActorId,
            [new PickedLineItem(satir.OrderId, satir.OrderItemId, satir.VariantId,
                satir.Quantity, satir.PickedBinId, true)]), ct);

        return Result.Success(new SingleItemScanResultDto(
            satir.OrderId, satir.OrderNumber, paket.Id, paketNo, siparis.FirmPlatformId));
    }
}
