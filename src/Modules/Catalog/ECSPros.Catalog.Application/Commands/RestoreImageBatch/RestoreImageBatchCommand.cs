using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.RestoreImageBatch;

public record RestoreImageBatchCommand(Guid ProductId, Guid BatchId) : IRequest<Result<int>>;

public class RestoreImageBatchCommandHandler : IRequestHandler<RestoreImageBatchCommand, Result<int>>
{
    private readonly ICatalogDbContext _db;

    public RestoreImageBatchCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<int>> Handle(RestoreImageBatchCommand request, CancellationToken ct)
    {
        var batchImages = await _db.ProductImages
            .Where(x => x.BatchId == request.BatchId
                && x.ProductId == request.ProductId
                && x.Status == ProductImageStatus.Archived)
            .ToListAsync(ct);

        if (!batchImages.Any())
            return Result.Failure<int>("Arşiv batch'i bulunamadı.");

        var imageSetId = batchImages.First().ImageSetId;
        var variantId = batchImages.First().VariantId;

        // Mevcut aktif olanları arşivle
        var currentActive = await _db.ProductImages
            .Where(x => x.ProductId == request.ProductId
                && x.ImageSetId == imageSetId
                && x.VariantId == variantId
                && x.Status == ProductImageStatus.Active)
            .ToListAsync(ct);

        foreach (var img in currentActive)
        {
            img.Status = ProductImageStatus.Archived;
            img.ArchivedAt = DateTime.UtcNow;
        }

        foreach (var img in batchImages)
        {
            img.Status = ProductImageStatus.Active;
            img.ArchivedAt = null;
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success(batchImages.Count);
    }
}
