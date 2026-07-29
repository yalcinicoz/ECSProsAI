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

        if (request.Statuses is { Count: > 0 })
            query = query.Where(o => request.Statuses.Contains(o.Status));

        if (request.MemberId.HasValue)
            query = query.Where(o => o.MemberId == request.MemberId);

        if (request.FirmPlatformId.HasValue)
            query = query.Where(o => o.FirmPlatformId == request.FirmPlatformId.Value);

        if (request.CreatedFrom.HasValue)
            query = query.Where(o => o.CreatedAt >= request.CreatedFrom.Value);

        if (request.CreatedTo.HasValue)
            query = query.Where(o => o.CreatedAt < request.CreatedTo.Value); // exclusive üst sınır

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(o =>
                o.OrderNumber.ToLower().Contains(term) ||
                o.ShippingRecipientName.ToLower().Contains(term));
        }

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
                o.CreatedAt,
                o.ShippingRecipientName))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedOrderResult(items, totalCount, request.Page, request.PageSize));
    }
}
