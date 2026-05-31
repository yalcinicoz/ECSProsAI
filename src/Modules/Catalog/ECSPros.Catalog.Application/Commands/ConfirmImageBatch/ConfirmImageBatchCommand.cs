using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.ConfirmImageBatch;

public record ConfirmImageBatchItem(Guid ImageId, int SortOrder, bool IsProductCover, bool IsVariantCover);

public record ConfirmImageBatchCommand(
    Guid ProductId,
    Guid BatchId,
    bool ReplaceSet,
    List<ConfirmImageBatchItem> ConfirmedImages) : IRequest<Result<int>>;

public class ConfirmImageBatchCommandHandler : IRequestHandler<ConfirmImageBatchCommand, Result<int>>
{
    private readonly ICatalogDbContext _db;

    public ConfirmImageBatchCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<int>> Handle(ConfirmImageBatchCommand request, CancellationToken ct)
    {
        var batchImages = await _db.ProductImages
            .Where(x => x.BatchId == request.BatchId && x.ProductId == request.ProductId)
            .ToListAsync(ct);

        if (!batchImages.Any())
            return Result.Failure<int>("Batch bulunamadı.");

        var confirmedIds = request.ConfirmedImages.Select(x => x.ImageId).ToHashSet();
        var imageSetId = batchImages.First().ImageSetId;
        var variantId = batchImages.First().VariantId;

        // ReplaceSet: mevcut aktif resimleri arşivle
        if (request.ReplaceSet)
        {
            var existing = await _db.ProductImages
                .Where(x => x.ProductId == request.ProductId
                    && x.ImageSetId == imageSetId
                    && x.VariantId == variantId
                    && x.Status == ProductImageStatus.Active
                    && x.BatchId != request.BatchId)
                .ToListAsync(ct);

            foreach (var img in existing)
            {
                img.Status = ProductImageStatus.Archived;
                img.ArchivedAt = DateTime.UtcNow;
            }
        }

        int activated = 0;
        foreach (var image in batchImages)
        {
            if (confirmedIds.Contains(image.Id))
            {
                var meta = request.ConfirmedImages.First(x => x.ImageId == image.Id);
                image.Status = ProductImageStatus.Active;
                image.SortOrder = meta.SortOrder;
                image.IsProductCover = meta.IsProductCover;
                image.IsVariantCover = meta.IsVariantCover;
                activated++;
            }
            else
            {
                image.Status = ProductImageStatus.Cancelled;
            }
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success(activated);
    }
}
