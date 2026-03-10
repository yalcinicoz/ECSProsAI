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
        var query = _context.Invoices.AsQueryable();

        if (request.OrderId.HasValue)
            query = query.Where(i => i.OrderId == request.OrderId.Value);

        if (!string.IsNullOrEmpty(request.Status))
            query = query.Where(i => i.Status == request.Status);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(i => new InvoiceListDto(
                i.Id, i.OrderId, i.InvoiceNumber, i.InvoiceType,
                i.InvoiceDate, i.RecipientName, i.GrandTotal,
                i.Status, i.IntegratorStatus, i.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<InvoiceListDto>(items, total, request.Page, request.PageSize));
    }
}
