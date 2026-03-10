using ECSPros.Integration.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Integration.Application.Queries.GetMarketplaceProducts;

public record GetMarketplaceProductsQuery(
    Guid FirmIntegrationId,
    string? SyncStatus = null,
    int Page = 1,
    int PageSize = 50) : IRequest<Result<PagedResult<MarketplaceProductDto>>>;

public record MarketplaceProductDto(
    Guid Id,
    Guid FirmIntegrationId,
    Guid VariantId,
    string ExternalId,
    string? ExternalBarcode,
    string SyncStatus,
    DateTime? LastSyncedAt,
    decimal? MarketplacePrice,
    int? MarketplaceStock,
    DateTime? StockSyncedAt);

public class GetMarketplaceProductsQueryHandler(IIntegrationDbContext db)
    : IRequestHandler<GetMarketplaceProductsQuery, Result<PagedResult<MarketplaceProductDto>>>
{
    public async Task<Result<PagedResult<MarketplaceProductDto>>> Handle(
        GetMarketplaceProductsQuery request, CancellationToken ct)
    {
        var q = db.MarketplaceProducts.AsNoTracking()
            .Where(x => x.FirmIntegrationId == request.FirmIntegrationId);

        if (!string.IsNullOrWhiteSpace(request.SyncStatus))
            q = q.Where(x => x.SyncStatus == request.SyncStatus);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(x => x.LastSyncedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new MarketplaceProductDto(
                x.Id, x.FirmIntegrationId, x.VariantId, x.ExternalId, x.ExternalBarcode,
                x.SyncStatus, x.LastSyncedAt, x.MarketplacePrice, x.MarketplaceStock, x.StockSyncedAt))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<MarketplaceProductDto>(items, total, request.Page, request.PageSize));
    }
}
