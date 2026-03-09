using ECSPros.Inventory.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Queries.GetWarehouses;

public class GetWarehousesQueryHandler : IRequestHandler<GetWarehousesQuery, Result<List<WarehouseDto>>>
{
    private readonly IInventoryDbContext _context;

    public GetWarehousesQueryHandler(IInventoryDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<WarehouseDto>>> Handle(GetWarehousesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Warehouses.AsQueryable();
        if (request.ActiveOnly)
            query = query.Where(w => w.IsActive);

        var items = await query
            .OrderBy(w => w.SortOrder)
            .Select(w => new WarehouseDto(w.Id, w.Code, w.NameI18n, w.WarehouseType, w.Address, w.IsSellableOnline, w.IsActive, w.SortOrder))
            .ToListAsync(cancellationToken);

        return Result.Success(items);
    }
}
