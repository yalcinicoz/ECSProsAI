using ECSPros.Finance.Application.Services;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Finance.Application.Queries.GetSupplierInvoices;

/// <summary>Tedarikçi fatura listesi. <c>Grid</c> verilirse DataGrid filtre/sıralama/arama (SupplierInvoiceGrid.Schema) uygulanır; Page/PageSize Grid'den alınır.</summary>
public record GetSupplierInvoicesQuery(
    Guid? CurrentAccountId,
    string? Status,
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    GridRequest? Grid = null
) : IRequest<Result<PagedResult<SupplierInvoiceSummaryDto>>>;

public record SupplierInvoiceSummaryDto(
    Guid Id,
    Guid CurrentAccountId,
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
        var query = SupplierInvoiceGrid.ApplyAll(_db.SupplierInvoices.AsNoTracking(),
            new SupplierInvoiceListFilters(request.CurrentAccountId, request.Status, request.Search), request.Grid);
        var page = request.Grid?.Page ?? request.Page;
        var pageSize = request.Grid?.PageSize ?? request.PageSize;

        var total = await query.CountAsync(ct);

        var items = await SupplierInvoiceGrid.Schema.ApplySort(query, request.Grid)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new SupplierInvoiceSummaryDto(
                i.Id, i.CurrentAccountId, i.InvoiceNumber, i.InvoiceDate, i.DueDate,
                i.GrandTotal, i.Status, i.Items.Count, i.CreatedAt))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<SupplierInvoiceSummaryDto>(items, total, page, pageSize));
    }
}
