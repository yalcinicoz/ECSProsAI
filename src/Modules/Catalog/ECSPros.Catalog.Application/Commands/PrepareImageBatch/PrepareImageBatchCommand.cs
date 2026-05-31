using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.PrepareImageBatch;

public record PrepareImageBatchCommand(
    Guid ProductId,
    Guid? VariantId,
    Guid ImageSetId,
    List<string> FileExtensions,   // ["jpg","jpg","png"] — sıra önemli
    bool ReplaceSet) : IRequest<Result<PrepareImageBatchResult>>;

public record PrepareImageBatchResult(
    Guid BatchId,
    List<PreparedImageItem> Images);

public record PreparedImageItem(Guid ImageId, string FileName);

public class PrepareImageBatchCommandHandler : IRequestHandler<PrepareImageBatchCommand, Result<PrepareImageBatchResult>>
{
    private readonly ICatalogDbContext _db;

    public PrepareImageBatchCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<PrepareImageBatchResult>> Handle(PrepareImageBatchCommand request, CancellationToken ct)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(x => x.Id == request.ProductId, ct);
        if (product is null)
            return Result.Failure<PrepareImageBatchResult>("Ürün bulunamadı.");

        var imageSet = await _db.ImageSets
            .FirstOrDefaultAsync(x => x.Id == request.ImageSetId, ct);
        if (imageSet is null)
            return Result.Failure<PrepareImageBatchResult>("ImageSet bulunamadı.");

        string? variantCode = null;
        if (request.VariantId.HasValue)
        {
            var variant = await _db.ProductVariants
                .FirstOrDefaultAsync(x => x.Id == request.VariantId.Value, ct);
            if (variant is null)
                return Result.Failure<PrepareImageBatchResult>("Varyant bulunamadı.");
            variantCode = variant.Sku.Replace("/", "-").Replace(" ", "_");
        }

        var batchId = Guid.NewGuid();
        var batchShort = batchId.ToString("N")[..8];
        var preparedItems = new List<PreparedImageItem>();

        for (int i = 0; i < request.FileExtensions.Count; i++)
        {
            var ext = request.FileExtensions[i].TrimStart('.').ToLowerInvariant();
            var variantPart = variantCode ?? "base";
            var fileName = $"{product.Code}_{imageSet.Code}_{variantPart}_{batchShort}_{(i + 1):D2}.{ext}";

            var image = new ProductImage
            {
                Id = Guid.NewGuid(),
                ProductId = request.ProductId,
                VariantId = request.VariantId,
                ImageSetId = request.ImageSetId,
                FileName = fileName,
                SortOrder = i + 1,
                Status = ProductImageStatus.Pending,
                BatchId = batchId
            };

            _db.ProductImages.Add(image);
            preparedItems.Add(new PreparedImageItem(image.Id, fileName));
        }

        await _db.SaveChangesAsync(ct);

        return Result.Success(
            new PrepareImageBatchResult(batchId, preparedItems));
    }
}
