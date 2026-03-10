using ECSPros.Fulfillment.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Queries.GetPackingStations;

public class GetPackingStationsQueryHandler : IRequestHandler<GetPackingStationsQuery, Result<List<PackingStationDto>>>
{
    private readonly IFulfillmentDbContext _context;

    public GetPackingStationsQueryHandler(IFulfillmentDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<PackingStationDto>>> Handle(GetPackingStationsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.PackingStations.AsQueryable();

        if (request.WarehouseId.HasValue)
            query = query.Where(s => s.WarehouseId == request.WarehouseId);

        if (request.ActiveOnly)
            query = query.Where(s => s.Status == "active");

        var items = await query
            .Select(s => new PackingStationDto(
                s.Id,
                s.WarehouseId,
                s.StationCode,
                s.StationName,
                s.SlotCount,
                s.IsObm,
                s.Status))
            .ToListAsync(cancellationToken);

        return Result.Success(items);
    }
}
