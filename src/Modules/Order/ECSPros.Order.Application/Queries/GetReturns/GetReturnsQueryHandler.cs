using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetReturns;

public class GetReturnsQueryHandler : IRequestHandler<GetReturnsQuery, Result<PagedResult<ReturnListDto>>>
{
    private readonly IOrderDbContext _context;

    public GetReturnsQueryHandler(IOrderDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedResult<ReturnListDto>>> Handle(GetReturnsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Returns.AsQueryable();

        if (request.OrderId.HasValue)
            query = query.Where(r => r.OrderId == request.OrderId.Value);

        if (request.MemberId.HasValue)
            query = query.Where(r => r.MemberId == request.MemberId.Value);

        if (!string.IsNullOrEmpty(request.Status))
            query = query.Where(r => r.Status == request.Status);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(r => new ReturnListDto(
                r.Id,
                r.ReturnNumber,
                r.OrderId,
                r.MemberId,
                r.ReturnType,
                r.Status,
                r.RefundMethod,
                r.RefundStatus,
                r.RefundAmount,
                r.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<ReturnListDto>(items, total, request.Page, request.PageSize));
    }
}
