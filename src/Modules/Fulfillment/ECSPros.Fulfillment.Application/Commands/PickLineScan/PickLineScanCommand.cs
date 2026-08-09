using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Domain.Entities;
using ECSPros.Fulfillment.Domain.Events;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.PickLineScan;

/// <summary>
/// OP2: personel toplama okutması — kendisine atanan, barkodu eşleşen, henüz tamamlanmamış
/// EN DÜŞÜK rota sıralı satıra +1 yazar. BinBarcode verilmişse fiili raf odur (K-15),
/// verilmemişse önerilen raf kabul edilir. Eşleşme yoksa hata (panel hata sesi çalar).
/// </summary>
public record PickLineScanCommand(
    Guid PlanId,
    string Barcode,
    Guid ActorId,
    string? BinBarcode = null) : IRequest<Result<PickScanResultDto>>;

public record PickScanResultDto(
    Guid LineId, string OrderNumber, string DisplayName, string Sku,
    int PickedQuantity, int Quantity, string LineStatus,
    int KalanSatir); // personelin bu görevde kalan (tamamlanmamış) satır sayısı

public class PickLineScanCommandHandler(
    IFulfillmentDbContext db,
    IOrderPickingReader reader,
    IPublisher publisher)
    : IRequestHandler<PickLineScanCommand, Result<PickScanResultDto>>
{
    public async Task<Result<PickScanResultDto>> Handle(PickLineScanCommand request, CancellationToken ct)
    {
        var barkod = request.Barcode.Trim();
        if (barkod.Length == 0) return Result.Failure<PickScanResultDto>("Barkod boş.");

        var satir = await db.PickingPlanLines
            .Where(l => l.PickingPlanId == request.PlanId
                        && l.AssignedTo == request.ActorId
                        && l.VariantBarcode == barkod
                        && l.PickedQuantity < l.Quantity
                        && (l.Status == "assigned" || l.Status == "pending"))
            .OrderBy(l => l.RouteOrder)
            .FirstOrDefaultAsync(ct);
        if (satir is null)
            return Result.Failure<PickScanResultDto>("Bu barkod size atanan toplanacak ürünlerle eşleşmedi.");

        Guid? fiiliBin = satir.SourceBinId;
        string? fiiliKod = satir.SourceBinCode;
        if (!string.IsNullOrWhiteSpace(request.BinBarcode))
        {
            var raf = await reader.GetBinByBarcodeAsync(request.BinBarcode.Trim(), ct);
            if (raf is null) return Result.Failure<PickScanResultDto>("Raf barkodu tanınmadı.");
            (fiiliBin, fiiliKod) = (raf.BinId, raf.Code);
        }

        var now = DateTime.UtcNow;
        satir.PickedQuantity += 1;
        satir.PickedBinId = fiiliBin;
        satir.PickedBinCode = fiiliKod;
        satir.PickedBy = request.ActorId;
        satir.PickedAt = now;
        satir.UpdatedAt = now;
        satir.UpdatedBy = request.ActorId;
        var tamam = satir.PickedQuantity >= satir.Quantity;
        if (tamam) satir.Status = "picked";

        db.OperationLogs.Add(new OperationLog
        {
            OrderId = satir.OrderId, OrderItemId = satir.OrderItemId,
            PickingPlanId = request.PlanId, Action = "line_picked",
            ActorId = request.ActorId, CreatedBy = request.ActorId,
            Detail = new Dictionary<string, object>
            {
                ["sku"] = satir.Sku, ["bin"] = fiiliKod ?? "",
                ["picked"] = satir.PickedQuantity, ["quantity"] = satir.Quantity
            }
        });
        await db.SaveChangesAsync(ct);

        await publisher.Publish(new PickingLinePickedEvent(request.PlanId, request.ActorId,
            [new PickedLineItem(satir.OrderId, satir.OrderItemId, satir.VariantId, 1, fiiliBin, false)]), ct);

        var kalan = await db.PickingPlanLines.CountAsync(l =>
            l.PickingPlanId == request.PlanId && l.AssignedTo == request.ActorId
            && l.PickedQuantity < l.Quantity && (l.Status == "assigned" || l.Status == "pending"), ct);

        return Result.Success(new PickScanResultDto(satir.Id, satir.OrderNumber, satir.DisplayName,
            satir.Sku, satir.PickedQuantity, satir.Quantity, satir.Status, kalan));
    }
}
