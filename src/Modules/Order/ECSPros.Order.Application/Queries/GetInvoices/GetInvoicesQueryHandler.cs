using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetInvoices;

public class GetInvoicesQueryHandler : IRequestHandler<GetInvoicesQuery, Result<PagedResult<InvoiceListDto>>>
{
    private readonly IOrderDbContext _context;

    public GetInvoicesQueryHandler(IOrderDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedResult<InvoiceListDto>>> Handle(GetInvoicesQuery request, CancellationToken cancellationToken)
    {
        // DataGrid F4 (2026-09-08): adlandırılmış filtreler + arama + grid filtreleri/sıralaması tek yerden (InvoiceGrid)
        var query = InvoiceGrid.ApplyAll(_context.Invoices.AsQueryable(),
            new InvoiceListFilters(request.OrderId, request.Status, request.Search), request.Grid);

        var total = await query.CountAsync(cancellationToken);
        var items = await InvoiceGrid.Schema.ApplySort(query, request.Grid)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(i => new InvoiceListDto(
                i.Id, i.OrderId, i.InvoiceNumber, i.InvoiceType,
                i.InvoiceDate, i.RecipientName, i.GrandTotal,
                i.Status, i.IntegratorStatus, i.CreatedAt,
                i.IntegratorInvoiceUrl != null && i.IntegratorInvoiceUrl != "",
                i.NumberSource, i.ExternalSource))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<InvoiceListDto>(items, total, request.Page, request.PageSize));
    }
}
