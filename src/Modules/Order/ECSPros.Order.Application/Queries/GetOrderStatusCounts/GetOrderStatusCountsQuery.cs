using ECSPros.Order.Application.Services;
using ECSPros.Order.Application.Queries.GetOrders;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetOrderStatusCounts;

/// <summary>
/// Verilen durumlar için sipariş sayılarını döner. Yalnız aktif (küçük) durum kümeleriyle
/// çağrılmalıdır — kapalı durumlar (delivered/cancelled) milyonlara ulaşacağından sayılmaz.
/// </summary>
/// <summary>Filters/Grid (2026-09-08, DataGrid F0): sekme sayaçları durum DIŞINDAKİ aktif filtrelerle (arama, tarih, ödeme…) tutarlı sayılır.</summary>
public record GetOrderStatusCountsQuery(List<string> Statuses, OrderListFilters? Filters = null, GridRequest? Grid = null) : IRequest<Result<Dictionary<string, int>>>;

public class GetOrderStatusCountsQueryHandler : IRequestHandler<GetOrderStatusCountsQuery, Result<Dictionary<string, int>>>
{
    private readonly IOrderDbContext _context;

    public GetOrderStatusCountsQueryHandler(IOrderDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Dictionary<string, int>>> Handle(GetOrderStatusCountsQuery request, CancellationToken cancellationToken)
    {
        var baseQuery = OrderGrid.ApplyAll(_context.Orders.AsQueryable(), request.Filters ?? new OrderListFilters(), request.Grid, includeStatus: false, db: _context);
        var counts = await baseQuery
            .Where(o => request.Statuses.Contains(o.Status))
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var result = request.Statuses.ToDictionary(
            s => s,
            s => counts.FirstOrDefault(c => c.Status == s)?.Count ?? 0);

        return Result.Success(result);
    }
}
