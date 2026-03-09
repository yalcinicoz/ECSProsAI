using ECSPros.Inventory.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Queries.GetStocks;

public class GetStocksQueryHandler : IRequestHandler<GetStocksQuery, Result<List<StockDto>>>
{
    private readonly IInventoryDbContext _context;

    public GetStocksQueryHandler(IInventoryDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<StockDto>>> Handle(GetStocksQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Stocks.AsQueryable();

        if (request.WarehouseId.HasValue)
            query = query.Where(s => s.WarehouseId == request.WarehouseId);

        if (request.VariantId.HasValue)
            query = query.Where(s => s.VariantId == request.VariantId);

        if (request.AvailableOnly)
            query = query.Where(s => s.Quantity > s.ReservedQuantity);

        var items = await query
            .Select(s => new StockDto(
                s.Id,
                s.VariantId,
                s.WarehouseId,
                s.StockType,
                s.Quantity,
                s.ReservedQuantity,
                s.Quantity - s.ReservedQuantity))
            .ToListAsync(cancellationToken);

        return Result.Success(items);
    }
}
