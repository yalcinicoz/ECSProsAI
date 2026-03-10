using ECSPros.Inventory.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Queries.GetWarehouseLocations;

public record GetWarehouseLocationsQuery(Guid WarehouseId, bool ActiveOnly = true) : IRequest<Result<List<WarehouseLocationDto>>>;

public record WarehouseLocationDto(
    Guid Id,
    Guid WarehouseId,
    string Code,
    string Barcode,
    string? Name,
    Guid? ParentId,
    string LocationType,
    int ReservePriority,
    int PickingOrder,
    bool IsActive,
    int SortOrder);

public class GetWarehouseLocationsQueryHandler : IRequestHandler<GetWarehouseLocationsQuery, Result<List<WarehouseLocationDto>>>
{
    private readonly IInventoryDbContext _db;

    public GetWarehouseLocationsQueryHandler(IInventoryDbContext db) => _db = db;

    public async Task<Result<List<WarehouseLocationDto>>> Handle(GetWarehouseLocationsQuery request, CancellationToken ct)
    {
        var query = _db.WarehouseLocations
            .Where(l => l.WarehouseId == request.WarehouseId);

        if (request.ActiveOnly)
            query = query.Where(l => l.IsActive);

        var locations = await query
            .OrderBy(l => l.SortOrder)
            .ThenBy(l => l.Code)
            .Select(l => new WarehouseLocationDto(
                l.Id, l.WarehouseId, l.Code, l.Barcode, l.Name,
                l.ParentId, l.LocationType, l.ReservePriority,
                l.PickingOrder, l.IsActive, l.SortOrder))
            .ToListAsync(ct);

        return Result.Success(locations);
    }
}
