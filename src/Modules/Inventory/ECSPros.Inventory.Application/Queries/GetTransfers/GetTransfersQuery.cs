using ECSPros.Inventory.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Queries.GetTransfers;

public record GetTransfersQuery(
    Guid? FromWarehouseId,
    Guid? ToWarehouseId,
    string? Status,
    string? TransferType,
    int Page = 1,
    int PageSize = 20
) : IRequest<Result<PagedResult<TransferSummaryDto>>>;

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
        var query = _db.TransferRequests.AsQueryable();

        if (request.FromWarehouseId.HasValue)
            query = query.Where(t => t.FromWarehouseId == request.FromWarehouseId.Value);

        if (request.ToWarehouseId.HasValue)
            query = query.Where(t => t.ToWarehouseId == request.ToWarehouseId.Value);

        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(t => t.Status == request.Status);

        if (!string.IsNullOrWhiteSpace(request.TransferType))
            query = query.Where(t => t.TransferType == request.TransferType);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
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
