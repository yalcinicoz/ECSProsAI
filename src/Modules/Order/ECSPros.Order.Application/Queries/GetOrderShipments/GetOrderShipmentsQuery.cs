using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetOrderShipments;

public record GetOrderShipmentsQuery(Guid OrderId) : IRequest<Result<List<ShipmentDto>>>;

public record ShipmentDto(
    Guid Id,
    Guid OrderId,
    Guid FirmIntegrationId,
    string ShipmentNumber,
    string? TrackingNumber,
    string? TrackingUrl,
    string Status,
    DateOnly? EstimatedDeliveryDate,
    DateTime? DeliveredAt,
    int PackageCount,
    decimal? TotalWeight,
    List<ShipmentEventDto> Events,
    DateTime CreatedAt);

public record ShipmentEventDto(Guid Id, string EventCode, string EventDescription, string? EventLocation, DateTime EventDate);

public class GetOrderShipmentsQueryHandler : IRequestHandler<GetOrderShipmentsQuery, Result<List<ShipmentDto>>>
{
    private readonly IOrderDbContext _db;

    public GetOrderShipmentsQueryHandler(IOrderDbContext db) => _db = db;

    public async Task<Result<List<ShipmentDto>>> Handle(GetOrderShipmentsQuery request, CancellationToken ct)
    {
        var shipments = await _db.Shipments
            .Include(s => s.Events)
            .Where(s => s.OrderId == request.OrderId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

        var dtos = shipments.Select(s => new ShipmentDto(
            s.Id, s.OrderId, s.FirmIntegrationId,
            s.ShipmentNumber, s.TrackingNumber, s.TrackingUrl,
            s.Status, s.EstimatedDeliveryDate, s.DeliveredAt,
            s.PackageCount, s.TotalWeight,
            s.Events.OrderByDescending(e => e.EventDate)
                .Select(e => new ShipmentEventDto(e.Id, e.EventCode, e.EventDescription, e.EventLocation, e.EventDate))
                .ToList(),
            s.CreatedAt)).ToList();

        return Result.Success(dtos);
    }
}
