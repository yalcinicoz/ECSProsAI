using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Queries.GetInvoices;

public record GetInvoicesQuery(
    Guid? OrderId = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<InvoiceListDto>>>;

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
    DateTime CreatedAt);
