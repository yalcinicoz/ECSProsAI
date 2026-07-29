using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.SplitOrderIntoPackages;

public class SplitOrderIntoPackagesCommandHandler
    : IRequestHandler<SplitOrderIntoPackagesCommand, Result<List<Guid>>>
{
    private static readonly string[] PackableOrderStatuses = ["confirmed", "processing"];

    private readonly IFulfillmentDbContext _context;
    private readonly IPackageNumberService _packageNumbers;
    private readonly IOrderPackagingReader _orders;

    public SplitOrderIntoPackagesCommandHandler(
        IFulfillmentDbContext context,
        IPackageNumberService packageNumbers,
        IOrderPackagingReader orders)
    {
        _context = context;
        _packageNumbers = packageNumbers;
        _orders = orders;
    }

    public async Task<Result<List<Guid>>> Handle(SplitOrderIntoPackagesCommand request, CancellationToken cancellationToken)
    {
        var order = await _orders.GetOrderAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result.Failure<List<Guid>>("Sipariş bulunamadı.");

        if (!PackableOrderStatuses.Contains(order.Status))
            return Result.Failure<List<Guid>>(
                $"'{order.Status}' durumundaki sipariş paketlenemez (onaylanmış olmalı).");

        if (order.Items.Count == 0)
            return Result.Failure<List<Guid>>("Siparişte paketlenecek kalem yok.");

        var mevcutPaket = await _context.Packages
            .AnyAsync(p => p.OrderId == request.OrderId, cancellationToken);
        if (mevcutPaket)
            return Result.Failure<List<Guid>>(
                "Bu sipariş zaten paketlenmiş; düzenleme paket ekranından yapılmalıdır.");

        // Tedarikçi grubu başına bir paket (tedarikçisiz kalemler tek grupta)
        var gruplar = order.Items
            .GroupBy(i => i.SupplierId)
            .OrderBy(g => g.Key.HasValue ? 0 : 1).ThenBy(g => g.Key)
            .ToList();

        var ids = new List<Guid>();
        var sequence = 0;
        foreach (var grup in gruplar)
        {
            var packageNumber = await _packageNumbers.GenerateAsync(order.FirmPlatformId, cancellationToken);
            var package = new Package
            {
                OrderId = order.OrderId,
                FirmPlatformId = order.FirmPlatformId,
                PackageNumber = packageNumber,
                SequenceInOrder = ++sequence,
                SupplierId = grup.Key,
                Barcode = packageNumber,
                Status = "packed",
                PackedAt = DateTime.UtcNow,
                PackedBy = request.PackedBy
            };
            foreach (var item in grup)
            {
                package.Items.Add(new PackageItem
                {
                    OrderItemId = item.OrderItemId,
                    VariantId = item.VariantId,
                    Quantity = item.Quantity
                });
            }
            _context.Packages.Add(package);
            ids.Add(package.Id);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(ids);
    }
}
