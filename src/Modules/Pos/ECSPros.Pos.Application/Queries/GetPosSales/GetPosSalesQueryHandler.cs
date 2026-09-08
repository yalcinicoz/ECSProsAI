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
        // DataGrid (2026-09-08): adlandırılmış filtreler + beyaz listeli grid filtre/sıralama (PosSaleGrid); Grid yoksa eski davranış.
        var query = PosSaleGrid.ApplyAll(_context.PosSales.AsNoTracking(),
            new PosSaleListFilters(request.SessionId, request.RegisterId, request.DateFrom, request.DateTo, request.Status, request.Grid?.Search), request.Grid);
        var page = request.Grid?.Page ?? request.Page;
        var pageSize = request.Grid?.PageSize ?? request.PageSize;

        var total = await query.CountAsync(cancellationToken);

        var items = await PosSaleGrid.Schema.ApplySort(query, request.Grid)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new PosSaleListDto(
                s.Id,
                s.SaleNumber,
                s.SessionId,
                s.RegisterId,
                s.MemberId,
                s.Status,
                s.GrandTotal,
                s.CreatedAt,
                s.Session.Register.Name))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<PosSaleListDto>(items, total, page, pageSize));
    }
}
