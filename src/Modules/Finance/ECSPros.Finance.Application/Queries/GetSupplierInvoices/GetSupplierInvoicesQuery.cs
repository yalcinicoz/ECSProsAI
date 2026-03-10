using ECSPros.Finance.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Finance.Application.Queries.GetSupplierInvoices;

public record GetSupplierInvoicesQuery(
    Guid? SupplierId,
    string? Status,
    int Page = 1,
    int PageSize = 20
) : IRequest<Result<PagedResult<SupplierInvoiceSummaryDto>>>;

public record SupplierInvoiceSummaryDto(
    Guid Id,
    Guid SupplierId,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    decimal GrandTotal,
    string Status,
    int ItemCount,
    DateTime CreatedAt);

public class GetSupplierInvoicesQueryHandler : IRequestHandler<GetSupplierInvoicesQuery, Result<PagedResult<SupplierInvoiceSummaryDto>>>
{
    private readonly IFinanceDbContext _db;

    public GetSupplierInvoicesQueryHandler(IFinanceDbContext db) => _db = db;

    public async Task<Result<PagedResult<SupplierInvoiceSummaryDto>>> Handle(GetSupplierInvoicesQuery request, CancellationToken ct)
    {
        var query = _db.SupplierInvoices.AsQueryable();

        if (request.SupplierId.HasValue)
            query = query.Where(i => i.SupplierId == request.SupplierId.Value);

        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(i => i.Status == request.Status);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(i => i.InvoiceDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(i => new SupplierInvoiceSummaryDto(
                i.Id, i.SupplierId, i.InvoiceNumber, i.InvoiceDate, i.DueDate,
                i.GrandTotal, i.Status, i.Items.Count, i.CreatedAt))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<SupplierInvoiceSummaryDto>(items, total, request.Page, request.PageSize));
    }
}
