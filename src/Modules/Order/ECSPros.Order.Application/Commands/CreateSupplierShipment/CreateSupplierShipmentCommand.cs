using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.CreateSupplierShipment;

/// <summary>
/// Satıcı API P2 (2026-08-11): satıcının "kargoladım" bildirimi — kendi taşıyıcısı ve takip
/// numarasıyla (K3 mod 2 seller_ships; fulfillment.write scope). Bizim taşıyıcı entegrasyonu
/// devrede değildir: FirmIntegrationId=Guid.Empty + serbest CarrierName; ApiStatus="external"
/// (outbox worker'a girmez, taşıyıcıya biz istek atmayız). Satıcı kalemlerinin
/// FinalScanQuantity'si dolar — TryMarkOrderShipped böylece karma siparişte bizim kalemler de
/// tamamlanınca siparişi 'shipped'e alır (K-17 kısmi kuralı korunur). Paket başına TEK
/// bildirim: aynı pakete ikinci shipment reddedilir.
/// </summary>
public record CreateSupplierShipmentCommand(
    Guid SupplierId,
    Guid OrderId,
    Guid PackageId,
    string PackageNumber,
    string CarrierName,
    string TrackingNumber,
    string? TrackingUrl,
    Guid ActorId) : IRequest<Result<SupplierShipmentDto>>;

public record SupplierShipmentDto(Guid ShipmentId, string ShipmentNumber, string PackageNumber, string TrackingNumber);

public class CreateSupplierShipmentCommandHandler(IOrderDbContext db)
    : IRequestHandler<CreateSupplierShipmentCommand, Result<SupplierShipmentDto>>
{
    private static readonly string[] BildirilebilirDurumlar = ["confirmed", "processing"];

    public async Task<Result<SupplierShipmentDto>> Handle(CreateSupplierShipmentCommand request, CancellationToken ct)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, ct);
        if (order is null) return Result.Failure<SupplierShipmentDto>("Sipariş bulunamadı.");

        var saticiKalemleri = order.Items.Where(i => i.SupplierId == request.SupplierId).ToList();
        if (saticiKalemleri.Count == 0)
            return Result.Failure<SupplierShipmentDto>("Siparişte size ait kalem yok.");

        if (!BildirilebilirDurumlar.Contains(order.Status))
            return Result.Failure<SupplierShipmentDto>(
                $"'{order.Status}' durumundaki sipariş için kargo bildirilemez (onaylanmış/işlemde olmalı).");

        var mevcut = await db.Shipments.AsNoTracking()
            .FirstOrDefaultAsync(s => s.PackageId == request.PackageId, ct);
        if (mevcut is not null)
            return Result.Failure<SupplierShipmentDto>(
                $"Bu paket için kargo zaten bildirildi (takip no: {mevcut.TrackingNumber ?? "-"}).");

        var shipment = new Shipment
        {
            OrderId = request.OrderId,
            PackageId = request.PackageId,
            FirmIntegrationId = Guid.Empty,          // taşıyıcı bizim katalogda değil (satıcının anlaşması)
            CarrierName = request.CarrierName,
            ShipmentNumber = $"SHP-{request.PackageNumber}",
            TrackingNumber = request.TrackingNumber,
            TrackingUrl = request.TrackingUrl,
            Status = "shipped",
            ApiStatus = "external",                  // outbox/worker bu kaydı taşıyıcıya göndermez
            PackageCount = 1,
            CreatedBy = request.ActorId
        };
        db.Shipments.Add(shipment);

        // Satıcı kalemleri "son kontrolden geçmiş" sayılır — sipariş geneli kargolama kararı
        // (TryMarkOrderShipped) bizim kalemlerin durumunu da gözetir.
        foreach (var kalem in saticiKalemleri)
        {
            if (kalem.FinalScanQuantity < kalem.Quantity)
                kalem.FinalScanQuantity = kalem.Quantity;
            kalem.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return Result.Success(new SupplierShipmentDto(
            shipment.Id, shipment.ShipmentNumber, request.PackageNumber, request.TrackingNumber));
    }
}
