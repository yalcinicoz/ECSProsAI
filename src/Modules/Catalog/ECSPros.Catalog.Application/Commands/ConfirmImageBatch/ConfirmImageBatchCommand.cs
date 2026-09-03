using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
    private readonly IImageUploadService _imageUploadService;
    private readonly Microsoft.Extensions.Logging.ILogger<ConfirmImageBatchCommandHandler> _logger;

    public ConfirmImageBatchCommandHandler(
        ICatalogDbContext db,
        IImageUploadService imageUploadService,
        Microsoft.Extensions.Logging.ILogger<ConfirmImageBatchCommandHandler> logger)
    {
        _db = db;
        _imageUploadService = imageUploadService;
        _logger = logger;
    }

    public async Task<Result<int>> Handle(ConfirmImageBatchCommand request, CancellationToken ct)
    {
        var batchImages = await _db.ProductImages
            .Where(x => x.BatchId == request.BatchId && x.ProductId == request.ProductId)
            .ToListAsync(ct);

        if (!batchImages.Any())
            return Result.Failure<int>("Batch bulunamadı.");

        var confirmedIds = request.ConfirmedImages.Select(x => x.ImageId).ToHashSet();
        if (confirmedIds.Count != request.ConfirmedImages.Count ||
            confirmedIds.Any(id => batchImages.All(x => x.Id != id)))
            return Result.Failure<int>("Onaylanan resim listesi batch ile uyuşmuyor.");

        var imageSetId = batchImages.First().ImageSetId;
        var variantId = batchImages.First().VariantId;
        var filesToDelete = new List<string>();

        // ReplaceSet: yalnız en az bir yeni resim doğrulandıysa mevcut aktifleri kaldır.
        // Fiziksel dosyalar DB değişikliği kalıcı olduktan sonra silinir.
        if (request.ReplaceSet && confirmedIds.Count > 0)
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
                img.Status = ProductImageStatus.Cancelled;
                img.IsDeleted = true;
                img.DeletedAt = DateTime.UtcNow;
                filesToDelete.Add(img.FileName);
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
                image.IsDeleted = true;
                image.DeletedAt = DateTime.UtcNow;
                filesToDelete.Add(image.FileName);
            }
        }

        await _db.SaveChangesAsync(ct);

        // Aynı fiziksel dosya başka aktif kayıtta kullanılıyorsa silme.
        foreach (var fileName in filesToDelete.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                // DB değişikliği commit edildikten sonra istemcinin bağlantıyı kapatması,
                // eski fiziksel dosya temizliğini veya başarılı confirm cevabını bozmamalı.
                var stillReferenced = await _db.ProductImages.AnyAsync(
                    x => x.Status == ProductImageStatus.Active && x.FileName == fileName,
                    CancellationToken.None);
                if (stillReferenced)
                    continue;

                var deleted = await _imageUploadService.DeleteAsync(fileName, CancellationToken.None);
                if (!deleted)
                    _logger.LogWarning("Catalog image fiziksel temizliği tamamlanamadı: {FileName}", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Catalog image fiziksel temizliği hata verdi: {FileName}", fileName);
            }
        }

        return Result.Success(activated);
    }
}
