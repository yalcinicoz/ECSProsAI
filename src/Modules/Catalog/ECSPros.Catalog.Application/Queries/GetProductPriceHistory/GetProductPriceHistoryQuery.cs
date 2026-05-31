using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetProductPriceHistory;

public record GetProductPriceHistoryQuery(Guid ProductId) : IRequest<Result<List<UnifiedPriceHistoryDto>>>;

public record UnifiedPriceHistoryDto(
    Guid Id,
    string Source,          // "product" | "channel"
    string PriceField,      // "base_price" | "base_cost" | "platform_price"
    string? FirmPlatformCode,
    string? VariantSku,
    decimal? OldValue,
    decimal? NewValue,
    DateTime ChangedAt,
    Guid? ChangedBy,
    string? ChangedByName);

public class GetProductPriceHistoryQueryHandler
    : IRequestHandler<GetProductPriceHistoryQuery, Result<List<UnifiedPriceHistoryDto>>>
{
    private readonly ICatalogDbContext _db;
    public GetProductPriceHistoryQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<List<UnifiedPriceHistoryDto>>> Handle(
        GetProductPriceHistoryQuery request, CancellationToken ct)
    {
        // Product-level history
        var productRows = await _db.ProductPriceHistories
            .Where(h => h.ProductId == request.ProductId)
            .Select(h => new UnifiedPriceHistoryDto(
                h.Id, "product", h.PriceField,
                null, null,
                h.OldValue, h.NewValue,
                h.ChangedAt, h.ChangedBy, h.ChangedByName))
            .ToListAsync(ct);

        // Channel-level (variant) history
        var variantIds = await _db.ProductVariants
            .Where(v => v.ProductId == request.ProductId)
            .Select(v => new { v.Id, v.Sku })
            .ToListAsync(ct);

        var idList = variantIds.Select(v => v.Id).ToList();
        var skuMap = variantIds.ToDictionary(v => v.Id, v => v.Sku);

        var channelRows = await _db.VariantPriceHistories
            .Where(h => idList.Contains(h.VariantId) && h.FirmPlatformId != null)
            .Select(h => new
            {
                h.Id, h.VariantId, h.FirmPlatformCode,
                h.OldValue, h.NewValue, h.ChangedAt,
                h.ChangedBy, h.ChangedByName,
            })
            .ToListAsync(ct);

        var channelDtos = channelRows.Select(h => new UnifiedPriceHistoryDto(
            h.Id, "channel", "platform_price",
            h.FirmPlatformCode,
            skuMap.GetValueOrDefault(h.VariantId),
            h.OldValue, h.NewValue,
            h.ChangedAt, h.ChangedBy, h.ChangedByName))
            .ToList();

        var all = productRows
            .Concat(channelDtos)
            .OrderByDescending(h => h.ChangedAt)
            .ToList();

        return Result.Success(all);
    }
}
