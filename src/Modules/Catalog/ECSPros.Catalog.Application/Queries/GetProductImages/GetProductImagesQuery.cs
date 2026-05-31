using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetProductImages;

public record GetProductImagesQuery(
    Guid ProductId,
    Guid? ImageSetId,
    Guid? VariantId,
    bool ApplyFallback = true) : IRequest<Result<List<ProductImageDto>>>;

public record ProductImageDto(
    Guid Id,
    Guid ProductId,
    Guid? VariantId,
    Guid ImageSetId,
    string ImageSetCode,
    string FileName,
    int SortOrder,
    bool IsProductCover,
    bool IsVariantCover,
    string Status,
    Guid BatchId);

public class GetProductImagesQueryHandler : IRequestHandler<GetProductImagesQuery, Result<List<ProductImageDto>>>
{
    private readonly ICatalogDbContext _db;

    public GetProductImagesQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<List<ProductImageDto>>> Handle(GetProductImagesQuery request, CancellationToken ct)
    {
        Guid? resolvedSetId = request.ImageSetId;

        if (resolvedSetId.HasValue && request.ApplyFallback)
        {
            resolvedSetId = await ResolveFallbackSetId(request.ProductId, resolvedSetId.Value, request.VariantId, ct);
        }

        var query = _db.ProductImages
            .Include(x => x.ImageSet)
            .Where(x => x.ProductId == request.ProductId && x.Status == ProductImageStatus.Active);

        if (resolvedSetId.HasValue)
            query = query.Where(x => x.ImageSetId == resolvedSetId.Value);

        if (request.VariantId.HasValue)
            query = query.Where(x => x.VariantId == request.VariantId.Value);

        var images = await query
            .OrderBy(x => x.SortOrder)
            .Select(x => new ProductImageDto(
                x.Id,
                x.ProductId,
                x.VariantId,
                x.ImageSetId,
                x.ImageSet.Code,
                x.FileName,
                x.SortOrder,
                x.IsProductCover,
                x.IsVariantCover,
                x.Status.ToString(),
                x.BatchId))
            .ToListAsync(ct);

        return Result<List<ProductImageDto>>.Success(images);
    }

    private async Task<Guid> ResolveFallbackSetId(Guid productId, Guid requestedSetId, Guid? variantId, CancellationToken ct)
    {
        // 1. Ürünün istenen sette resmi var mı?
        bool hasImages = await _db.ProductImages
            .AnyAsync(x => x.ProductId == productId
                && x.ImageSetId == requestedSetId
                && (variantId == null || x.VariantId == variantId)
                && x.Status == ProductImageStatus.Active, ct);

        if (hasImages)
            return requestedSetId;

        // 2. ProductImageSetMapping override var mı?
        var mapping = await _db.ProductImageSetMappings
            .FirstOrDefaultAsync(x => x.ProductId == productId && x.ForSetId == requestedSetId, ct);

        if (mapping != null)
        {
            bool hasMappedImages = await _db.ProductImages
                .AnyAsync(x => x.ProductId == productId
                    && x.ImageSetId == mapping.UseSetId
                    && (variantId == null || x.VariantId == variantId)
                    && x.Status == ProductImageStatus.Active, ct);

            if (hasMappedImages)
                return mapping.UseSetId;
        }

        // 3. Set zinciri: FallbackSetId takip et
        var visitedSets = new HashSet<Guid> { requestedSetId };
        var currentSet = await _db.ImageSets.FirstOrDefaultAsync(x => x.Id == requestedSetId, ct);

        while (currentSet?.FallbackSetId != null && !visitedSets.Contains(currentSet.FallbackSetId.Value))
        {
            visitedSets.Add(currentSet.FallbackSetId.Value);
            bool hasFallbackImages = await _db.ProductImages
                .AnyAsync(x => x.ProductId == productId
                    && x.ImageSetId == currentSet.FallbackSetId
                    && (variantId == null || x.VariantId == variantId)
                    && x.Status == ProductImageStatus.Active, ct);

            if (hasFallbackImages)
                return currentSet.FallbackSetId.Value;

            currentSet = await _db.ImageSets.FirstOrDefaultAsync(x => x.Id == currentSet.FallbackSetId.Value, ct);
        }

        // 4. Default set
        var defaultSet = await _db.ImageSets.FirstOrDefaultAsync(x => x.IsDefault, ct);
        if (defaultSet != null)
        {
            bool hasDefaultImages = await _db.ProductImages
                .AnyAsync(x => x.ProductId == productId
                    && x.ImageSetId == defaultSet.Id
                    && (variantId == null || x.VariantId == variantId)
                    && x.Status == ProductImageStatus.Active, ct);

            if (hasDefaultImages)
                return defaultSet.Id;
        }

        // 5. Herhangi bir set — istenen seti döndür (boş döner)
        return requestedSetId;
    }
}
