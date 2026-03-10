using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetQuotes;

public class GetQuotesQueryHandler : IRequestHandler<GetQuotesQuery, Result<PagedResult<QuoteListDto>>>
{
    private readonly IOrderDbContext _context;

    public GetQuotesQueryHandler(IOrderDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedResult<QuoteListDto>>> Handle(GetQuotesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Quotes.AsQueryable();

        if (request.MemberId.HasValue)
            query = query.Where(q => q.MemberId == request.MemberId.Value);

        if (!string.IsNullOrEmpty(request.Status))
            query = query.Where(q => q.Status == request.Status);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(q => q.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(q => new QuoteListDto(
                q.Id, q.QuoteNumber, q.MemberId, q.Status,
                q.CurrencyCode, q.GrandTotal, q.ValidUntil,
                q.SentAt, q.ConvertedOrderId, q.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<QuoteListDto>(items, total, request.Page, request.PageSize));
    }
}
