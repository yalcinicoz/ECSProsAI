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
        // DataGrid F0 (2026-09-08): adlandırılmış filtreler + global arama + beyaz listeli grid filtreleri TEK yerden (OrderGrid).
        var filters = new OrderListFilters(request.Status, request.Statuses, request.MemberId, request.FirmPlatformId,
            request.CreatedFrom, request.CreatedTo, request.PaymentMethod, request.PaymentCollected, request.Search);
        var query = OrderGrid.ApplyAll(_context.Orders.AsQueryable(), filters, request.Grid, db: _context);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await OrderGrid.Schema.ApplySort(query, request.Grid)
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
                o.ShippingRecipientName,
                o.PaymentMethod,
                o.RequestedCargoName,
                ECSPros.Shared.Kernel.Grid.GridJson.TextObj(o.CustomerNotes, "note"),
                o.InternalNotes))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedOrderResult(items, totalCount, request.Page, request.PageSize));
    }
}
