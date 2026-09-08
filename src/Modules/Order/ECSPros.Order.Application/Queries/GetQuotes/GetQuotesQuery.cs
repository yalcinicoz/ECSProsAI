using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;

namespace ECSPros.Order.Application.Queries.GetQuotes;

/// <summary>Teklif listesi. <c>Grid</c> verilirse DataGrid filtre/sıralama/arama (QuoteGrid.Schema) uygulanır; Page/PageSize Grid'den alınır.</summary>
public record GetQuotesQuery(
    Guid? MemberId = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    GridRequest? Grid = null) : IRequest<Result<PagedResult<QuoteListDto>>>;

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
