using ECSPros.Inventory.Application.Services;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Queries.GetTransfers;

public record GetTransfersQuery(
    Guid? FromWarehouseId,
    Guid? ToWarehouseId,
    string? Status,
    string? TransferType,
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    GridRequest? Grid = null
) : IRequest<Result<PagedResult<TransferSummaryDto>>>;
    // Search/Grid (2026-09-09, DataGrid): global arama + beyaz listeli f.* filtreleri + sort/dir (TransferGrid.Schema)

public record TransferSummaryDto(
    Guid Id,
    string Code,
    Guid FromWarehouseId,
    string FromWarehouseCode,
    Guid ToWarehouseId,
    string ToWarehouseCode,
    string TransferType,
    string Status,
    int ItemCount,
    DateTime RequestedAt,
    DateTime CreatedAt);

public class GetTransfersQueryHandler : IRequestHandler<GetTransfersQuery, Result<PagedResult<TransferSummaryDto>>>
{
    private readonly IInventoryDbContext _db;

    public GetTransfersQueryHandler(IInventoryDbContext db) => _db = db;

    public async Task<Result<PagedResult<TransferSummaryDto>>> Handle(GetTransfersQuery request, CancellationToken ct)
    {
        // Adlandırılmış + grid filtreleri TEK yerden (liste ve Excel aynı sonucu verir).
        var query = TransferGrid.ApplyAll(
            _db.TransferRequests.AsNoTracking(),
            new TransferListFilters(request.FromWarehouseId, request.ToWarehouseId, request.Status, request.TransferType, request.Search),
            request.Grid);

        var total = await query.CountAsync(ct);

        var items = await TransferGrid.Schema.ApplySort(query, request.Grid)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new TransferSummaryDto(
                t.Id, t.Code,
                t.FromWarehouseId, t.FromWarehouse.Code,
                t.ToWarehouseId, t.ToWarehouse.Code,
                t.TransferType, t.Status,
                t.Items.Count,
                t.RequestedAt, t.CreatedAt))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<TransferSummaryDto>(items, total, request.Page, request.PageSize));
    }
}
