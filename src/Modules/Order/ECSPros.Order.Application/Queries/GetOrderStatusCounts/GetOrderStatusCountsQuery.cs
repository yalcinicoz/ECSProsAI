using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetOrderStatusCounts;

/// <summary>
/// Verilen durumlar için sipariş sayılarını döner. Yalnız aktif (küçük) durum kümeleriyle
/// çağrılmalıdır — kapalı durumlar (delivered/cancelled) milyonlara ulaşacağından sayılmaz.
/// </summary>
public record GetOrderStatusCountsQuery(List<string> Statuses) : IRequest<Result<Dictionary<string, int>>>;

public class GetOrderStatusCountsQueryHandler : IRequestHandler<GetOrderStatusCountsQuery, Result<Dictionary<string, int>>>
{
    private readonly IOrderDbContext _context;

    public GetOrderStatusCountsQueryHandler(IOrderDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Dictionary<string, int>>> Handle(GetOrderStatusCountsQuery request, CancellationToken cancellationToken)
    {
        var counts = await _context.Orders
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
