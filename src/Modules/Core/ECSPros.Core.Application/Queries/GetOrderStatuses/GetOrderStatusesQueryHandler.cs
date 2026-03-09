using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Queries.GetOrderStatuses;

public class GetOrderStatusesQueryHandler : IRequestHandler<GetOrderStatusesQuery, Result<List<OrderStatusDto>>>
{
    private readonly ICoreDbContext _context;

    public GetOrderStatusesQueryHandler(ICoreDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<OrderStatusDto>>> Handle(GetOrderStatusesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.OrderStatuses.AsQueryable();
        if (request.ActiveOnly)
            query = query.Where(x => x.IsActive);

        var items = await query
            .OrderBy(x => x.SortOrder)
            .Select(x => new OrderStatusDto(x.Id, x.Code, x.NameI18n, x.Color, x.IsActive, x.SortOrder))
            .ToListAsync(cancellationToken);

        return Result.Success(items);
    }
}
