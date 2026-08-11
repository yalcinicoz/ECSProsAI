using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.EnsureSupplierPackage;

/// <summary>
/// Satıcı API P2 (2026-08-11): satıcının kargo bildiriminden önce paketi garanti eder —
/// satıcı kalemleri bizim operasyondan geçmediği (seller_ships) için paket burada, kanal
/// serisinden numarayla oluşturulur. Sipariş için satıcının paketi zaten varsa o döner
/// (idempotent). Kalemler siparişteki satıcı kalemlerinin tamamıdır (kısmi gönderim P2
/// kapsamı dışı — tek pakette tek bildirim).
/// </summary>
public record EnsureSupplierPackageCommand(
    Guid OrderId,
    Guid SupplierId,
    Guid PackedBy) : IRequest<Result<SupplierPackageRefDto>>;

public record SupplierPackageRefDto(Guid PackageId, string PackageNumber, bool AlreadyExisted, Guid? ShipmentId);

public class EnsureSupplierPackageCommandHandler(
    IFulfillmentDbContext context,
    IPackageNumberService packageNumbers,
    IOrderPackagingReader orders)
    : IRequestHandler<EnsureSupplierPackageCommand, Result<SupplierPackageRefDto>>
{
    public async Task<Result<SupplierPackageRefDto>> Handle(EnsureSupplierPackageCommand request, CancellationToken ct)
    {
        var mevcut = await context.Packages.AsNoTracking()
            .FirstOrDefaultAsync(p => p.OrderId == request.OrderId && p.SupplierId == request.SupplierId, ct);
        if (mevcut is not null)
            return Result.Success(new SupplierPackageRefDto(mevcut.Id, mevcut.PackageNumber, true, mevcut.ShipmentId));

        var order = await orders.GetOrderAsync(request.OrderId, ct);
        if (order is null)
            return Result.Failure<SupplierPackageRefDto>("Sipariş bulunamadı.");

        var kalemler = order.Items.Where(i => i.SupplierId == request.SupplierId).ToList();
        if (kalemler.Count == 0)
            return Result.Failure<SupplierPackageRefDto>("Siparişte size ait kalem yok.");

        var mevcutPaketSayisi = await context.Packages
            .CountAsync(p => p.OrderId == request.OrderId, ct);

        var packageNumber = await packageNumbers.GenerateAsync(order.FirmPlatformId, ct);
        var package = new Package
        {
            OrderId = request.OrderId,
            FirmPlatformId = order.FirmPlatformId,
            PackageNumber = packageNumber,
            SequenceInOrder = mevcutPaketSayisi + 1,
            SupplierId = request.SupplierId,
            Barcode = packageNumber,
            Status = "packed",
            PackedAt = DateTime.UtcNow,
            PackedBy = request.PackedBy
        };
        foreach (var kalem in kalemler)
        {
            package.Items.Add(new PackageItem
            {
                OrderItemId = kalem.OrderItemId,
                VariantId = kalem.VariantId,
                Quantity = kalem.Quantity
            });
        }
        context.Packages.Add(package);
        await context.SaveChangesAsync(ct);
        return Result.Success(new SupplierPackageRefDto(package.Id, packageNumber, false, null));
    }
}
