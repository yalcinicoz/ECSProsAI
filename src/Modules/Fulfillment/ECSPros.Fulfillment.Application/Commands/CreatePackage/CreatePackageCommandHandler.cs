using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.CreatePackage;

public class CreatePackageCommandHandler : IRequestHandler<CreatePackageCommand, Result<Guid>>
{
    private readonly IFulfillmentDbContext _context;
    private readonly IPackageNumberService _packageNumbers;
    private readonly IOrderPackagingReader _orders;

    public CreatePackageCommandHandler(
        IFulfillmentDbContext context,
        IPackageNumberService packageNumbers,
        IOrderPackagingReader orders)
    {
        _context = context;
        _packageNumbers = packageNumbers;
        _orders = orders;
    }

    public async Task<Result<Guid>> Handle(CreatePackageCommand request, CancellationToken cancellationToken)
    {
        var order = await _orders.GetOrderAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result.Failure<Guid>("Sipariş bulunamadı.");

        decimal? desi = request.Desi;
        if (!desi.HasValue && request.Width.HasValue && request.Height.HasValue && request.Length.HasValue)
            desi = request.Width.Value * request.Height.Value * request.Length.Value / 3000m;

        var packageNumber = await _packageNumbers.GenerateAsync(order.FirmPlatformId, cancellationToken);
        var sequence = await _context.Packages
            .Where(p => p.OrderId == request.OrderId)
            .MaxAsync(p => (int?)p.SequenceInOrder, cancellationToken) ?? 0;

        var package = new Package
        {
            OrderId = request.OrderId,
            FirmPlatformId = order.FirmPlatformId,
            ShipmentId = request.ShipmentId,
            PackageNumber = packageNumber,
            SequenceInOrder = sequence + 1,
            SupplierId = request.SupplierId,
            Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? packageNumber : request.Barcode!,
            Weight = request.Weight,
            Width = request.Width,
            Height = request.Height,
            Length = request.Length,
            Desi = desi,
            Status = "packed",
            PackedAt = DateTime.UtcNow,
            PackedBy = request.PackedBy
        };

        foreach (var item in request.Items ?? [])
        {
            package.Items.Add(new PackageItem
            {
                OrderItemId = item.OrderItemId,
                VariantId = item.VariantId,
                Quantity = item.Quantity
            });
        }

        _context.Packages.Add(package);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(package.Id);
    }
}
