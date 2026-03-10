using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetOrders;

public class GetOrdersQueryHandler : IRequestHandler<GetOrdersQuery, Result<PagedOrderResult>>
{
    private readonly IOrderDbContext _context;

    public GetOrdersQueryHandler(IOrderDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedOrderResult>> Handle(GetOrdersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Orders.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(o => o.Status == request.Status);

        if (request.MemberId.HasValue)
            query = query.Where(o => o.MemberId == request.MemberId);

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(o => o.OrderNumber.Contains(request.Search));

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(o => new OrderListDto(
                o.Id,
                o.OrderNumber,
                o.MemberId,
                o.Status,
                o.PaymentStatus,
                o.GrandTotal,
                o.CurrencyCode,
                o.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedOrderResult(items, totalCount, request.Page, request.PageSize));
    }
}
