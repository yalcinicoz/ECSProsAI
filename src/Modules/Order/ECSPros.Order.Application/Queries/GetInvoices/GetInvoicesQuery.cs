using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;

namespace ECSPros.Order.Application.Queries.GetInvoices;

public record GetInvoicesQuery(
    Guid? OrderId = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    GridRequest? Grid = null) : IRequest<Result<PagedResult<InvoiceListDto>>>;
    // Search/Grid (2026-09-08, DataGrid F4): global arama + beyaz listeli f.* filtreleri + sort/dir (InvoiceGrid.Schema)

public record InvoiceListDto(
    Guid Id,
    Guid OrderId,
    string InvoiceNumber,
    string InvoiceType,
    DateTime InvoiceDate,
    string RecipientName,
    decimal GrandTotal,
    string Status,
    string IntegratorStatus,
    DateTime CreatedAt,
    bool HasIntegratorPdf = false, // H1 additive: entegratör PDF'i var mı (URL sızmaz)
    string NumberSource = "internal", // FE1: internal | erp | marketplace | integrator
    string? ExternalSource = null);
