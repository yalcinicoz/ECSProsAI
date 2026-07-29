using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetGiftCards;

// Panel hediye kartı listesi — koda/duruma göre filtrelenebilir, sayfalı.

public record GetGiftCardsQuery(
    string? Status = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<GiftCardListDto>>>;

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
        var query = _context.GiftCards.AsQueryable();

        if (!string.IsNullOrEmpty(request.Status))
            query = query.Where(g => g.Status == request.Status);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var aranan = request.Search.Trim().ToUpper();
            query = query.Where(g => g.Code.ToUpper().Contains(aranan));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(g => g.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(g => new GiftCardListDto(
                g.Id, g.Code, g.OriginalAmount, g.RemainingAmount, g.CurrencyCode,
                g.ValidFrom, g.ValidUntil, g.IsSingleUse, g.CreatedForMemberId,
                g.Status, g.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<GiftCardListDto>(items, total, request.Page, request.PageSize));
    }
}
