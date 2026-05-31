using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetProductImageCoverageReport;

public record GetProductImageCoverageReportQuery(Guid? ImageSetId) : IRequest<Result<CoverageReportDto>>;

public record MissingVariantItem(Guid ProductId, string ProductCode, Guid VariantId, string VariantSku);
public record PartialProductItem(Guid ProductId, string ProductCode, int MissingVariantCount, int TotalVariantCount);
public record CoverageReportDto(List<MissingVariantItem> MissingVariants, List<PartialProductItem> PartialProducts);

public class GetProductImageCoverageReportQueryHandler
    : IRequestHandler<GetProductImageCoverageReportQuery, Result<CoverageReportDto>>
{
    private readonly ICatalogDbContext _db;

    public GetProductImageCoverageReportQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<CoverageReportDto>> Handle(GetProductImageCoverageReportQuery request, CancellationToken ct)
    {
        var variants = await _db.ProductVariants
            .Include(x => x.Product)
            .Where(x => x.Product.IsActive)
            .Select(x => new { x.Id, x.Sku, x.ProductId, ProductCode = x.Product.Code })
            .ToListAsync(ct);

        var imageQuery = _db.ProductImages
            .Where(x => x.Status == ProductImageStatus.Active);

        if (request.ImageSetId.HasValue)
            imageQuery = imageQuery.Where(x => x.ImageSetId == request.ImageSetId.Value);

        var variantIdsWithImages = await imageQuery
            .Where(x => x.VariantId != null)
            .Select(x => x.VariantId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var variantIdsWithImagesSet = variantIdsWithImages.ToHashSet();

        var missingVariants = variants
            .Where(v => !variantIdsWithImagesSet.Contains(v.Id))
            .Select(v => new MissingVariantItem(v.ProductId, v.ProductCode, v.Id, v.Sku))
            .ToList();

        var partialProducts = missingVariants
            .GroupBy(x => x.ProductId)
            .Select(g =>
            {
                var total = variants.Count(v => v.ProductId == g.Key);
                return new PartialProductItem(g.Key, g.First().ProductCode, g.Count(), total);
            })
            .Where(x => x.MissingVariantCount < x.TotalVariantCount)  // kısmen eksik
            .ToList();

        // Sadece tamamen eksik olanlar missingVariants'ta kalsın
        var allMissingProductIds = missingVariants.Select(x => x.ProductId).ToHashSet();
        var partialProductIds = partialProducts.Select(x => x.ProductId).ToHashSet();
        var fullyMissingProductIds = allMissingProductIds.Except(partialProductIds).ToHashSet();

        var filteredMissingVariants = missingVariants
            .Where(x => fullyMissingProductIds.Contains(x.ProductId) || partialProductIds.Contains(x.ProductId))
            .ToList();

        return Result.Success(new CoverageReportDto(filteredMissingVariants, partialProducts));
    }
}
