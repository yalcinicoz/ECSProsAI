using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.MarkShipped;

public class MarkShippedCommandHandler : IRequestHandler<MarkShippedCommand, Result<Guid>>
{
    private readonly IOrderDbContext _context;
    private readonly IPublisher _publisher;

    public MarkShippedCommandHandler(IOrderDbContext context, IPublisher publisher)
    {
        _context = context;
        _publisher = publisher;
    }

    public async Task<Result<Guid>> Handle(MarkShippedCommand request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order is null)
            return Result.Failure<Guid>("Sipariş bulunamadı.");

        try
        {
            order.MarkShipped(request.UpdatedBy);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<Guid>(ex.Message);
        }

        var shipmentNumber = $"SHP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";

        var shipment = new Shipment
        {
            OrderId = order.Id,
            FirmIntegrationId = request.FirmIntegrationId ?? Guid.Empty,
            ShipmentNumber = shipmentNumber,
            TrackingNumber = request.TrackingNumber,
            Status = "shipped",
            ApiStatus = "manual",
            PackageCount = request.PackageCount,
            CreatedBy = request.UpdatedBy
        };

        _context.Shipments.Add(shipment);
        await _context.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in order.DomainEvents)
            await _publisher.Publish(domainEvent, cancellationToken);

        order.ClearDomainEvents();

        return Result.Success(shipment.Id);
    }
}
