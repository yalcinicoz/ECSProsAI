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
        // DataGrid F4 (2026-09-08): adlandırılmış filtreler + arama + grid filtreleri/sıralaması tek yerden (ReturnGrid)
        var query = ReturnGrid.ApplyAll(_context.Returns.AsQueryable(),
            new ReturnListFilters(request.OrderId, request.MemberId, request.Status, request.Search), request.Grid);

        var total = await query.CountAsync(cancellationToken);

        var items = await ReturnGrid.Schema.ApplySort(query, request.Grid)
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
                r.CreatedAt,
                r.CargoReturnCode,
                r.Order.OrderNumber,
                r.RefundNotApplicableReason))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<ReturnListDto>(items, total, request.Page, request.PageSize));
    }
}
