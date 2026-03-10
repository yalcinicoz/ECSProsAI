using ECSPros.Inventory.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Queries.GetReservations;

public record GetReservationsQuery(
    Guid? WarehouseId,
    Guid? VariantId,
    string? ReferenceType,
    Guid? ReferenceId,
    string? Status
) : IRequest<Result<List<ReservationDto>>>;

public record ReservationDto(
    Guid Id,
    Guid StockId,
    Guid VariantId,
    Guid WarehouseId,
    Guid? LocationId,
    int Quantity,
    string ReferenceType,
    Guid ReferenceId,
    string Status,
    DateTime CreatedAt);

public class GetReservationsQueryHandler : IRequestHandler<GetReservationsQuery, Result<List<ReservationDto>>>
{
    private readonly IInventoryDbContext _db;

    public GetReservationsQueryHandler(IInventoryDbContext db) => _db = db;

    public async Task<Result<List<ReservationDto>>> Handle(GetReservationsQuery request, CancellationToken ct)
    {
        var query = _db.StockReservations.AsQueryable();

        if (request.WarehouseId.HasValue)
            query = query.Where(r => r.WarehouseId == request.WarehouseId.Value);

        if (request.VariantId.HasValue)
            query = query.Where(r => r.VariantId == request.VariantId.Value);

        if (!string.IsNullOrWhiteSpace(request.ReferenceType))
            query = query.Where(r => r.ReferenceType == request.ReferenceType);

        if (request.ReferenceId.HasValue)
            query = query.Where(r => r.ReferenceId == request.ReferenceId.Value);

        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(r => r.Status == request.Status);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReservationDto(
                r.Id, r.StockId, r.VariantId, r.WarehouseId, r.LocationId,
                r.Quantity, r.ReferenceType, r.ReferenceId, r.Status, r.CreatedAt))
            .ToListAsync(ct);

        return Result.Success(items);
    }
}
