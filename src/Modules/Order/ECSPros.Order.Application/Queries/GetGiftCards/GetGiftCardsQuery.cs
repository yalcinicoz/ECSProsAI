using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetGiftCards;

// Panel hediye kartı listesi — koda/duruma göre filtrelenebilir, sayfalı. `Grid` verilirse DataGrid filtre/sıralama (GiftCardGrid.Schema) uygulanır.

public record GetGiftCardsQuery(
    string? Status = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20,
    GridRequest? Grid = null) : IRequest<Result<PagedResult<GiftCardListDto>>>;

public record GiftCardListDto(
    Guid Id,
    string Code,
    decimal OriginalAmount,
    decimal RemainingAmount,
    string CurrencyCode,
    DateOnly ValidFrom,
    DateOnly? ValidUntil,
    bool IsSingleUse,
    Guid? CreatedForMemberId,
    string Status,
    DateTime CreatedAt);

public class GetGiftCardsQueryHandler : IRequestHandler<GetGiftCardsQuery, Result<PagedResult<GiftCardListDto>>>
{
    private readonly IOrderDbContext _context;

    public GetGiftCardsQueryHandler(IOrderDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedResult<GiftCardListDto>>> Handle(GetGiftCardsQuery request, CancellationToken cancellationToken)
    {
        var query = GiftCardGrid.ApplyAll(_context.GiftCards.AsNoTracking(), new GiftCardListFilters(request.Status, request.Search), request.Grid);
        var page = request.Grid?.Page ?? request.Page;
        var pageSize = request.Grid?.PageSize ?? request.PageSize;

        var total = await query.CountAsync(cancellationToken);
        var items = await GiftCardGrid.Schema.ApplySort(query, request.Grid)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new GiftCardListDto(
                g.Id, g.Code, g.OriginalAmount, g.RemainingAmount, g.CurrencyCode,
                g.ValidFrom, g.ValidUntil, g.IsSingleUse, g.CreatedForMemberId,
                g.Status, g.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<GiftCardListDto>(items, total, page, pageSize));
    }
}
