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
        var filters = new QuoteListFilters(request.MemberId, request.Status, request.Search);
        var query = QuoteGrid.ApplyAll(_context.Quotes.AsNoTracking(), filters, request.Grid);
        var page = request.Grid?.Page ?? request.Page;
        var pageSize = request.Grid?.PageSize ?? request.PageSize;

        var total = await query.CountAsync(cancellationToken);
        var items = await QuoteGrid.Schema.ApplySort(query, request.Grid)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(q => new QuoteListDto(
                q.Id, q.QuoteNumber, q.MemberId, q.Status,
                q.CurrencyCode, q.GrandTotal, q.ValidUntil,
                q.SentAt, q.ConvertedOrderId, q.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<QuoteListDto>(items, total, page, pageSize));
    }
}
