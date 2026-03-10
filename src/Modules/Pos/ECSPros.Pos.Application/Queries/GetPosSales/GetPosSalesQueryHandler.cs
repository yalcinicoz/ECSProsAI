using ECSPros.Pos.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Pos.Application.Queries.GetPosSales;

public class GetPosSalesQueryHandler : IRequestHandler<GetPosSalesQuery, Result<PagedResult<PosSaleListDto>>>
{
    private readonly IPosDbContext _context;

    public GetPosSalesQueryHandler(IPosDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedResult<PosSaleListDto>>> Handle(GetPosSalesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.PosSales.AsQueryable();

        if (request.SessionId.HasValue)
            query = query.Where(s => s.SessionId == request.SessionId.Value);

        if (request.RegisterId.HasValue)
            query = query.Where(s => s.RegisterId == request.RegisterId.Value);

        if (request.DateFrom.HasValue)
            query = query.Where(s => s.CreatedAt >= request.DateFrom.Value);

        if (request.DateTo.HasValue)
            query = query.Where(s => s.CreatedAt <= request.DateTo.Value);

        if (!string.IsNullOrEmpty(request.Status))
            query = query.Where(s => s.Status == request.Status);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new PosSaleListDto(
                s.Id,
                s.SaleNumber,
                s.SessionId,
                s.RegisterId,
                s.MemberId,
                s.Status,
                s.GrandTotal,
                s.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<PosSaleListDto>(items, total, request.Page, request.PageSize));
    }
}
