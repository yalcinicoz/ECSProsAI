using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Queries.GetQuotes;

public record GetQuotesQuery(
    Guid? MemberId = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<QuoteListDto>>>;

public record QuoteListDto(
    Guid Id,
    string QuoteNumber,
    Guid MemberId,
    string Status,
    string CurrencyCode,
    decimal GrandTotal,
    DateTime ValidUntil,
    DateTime? SentAt,
    Guid? ConvertedOrderId,
    DateTime CreatedAt);
