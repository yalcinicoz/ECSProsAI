using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.PrepareVideoBatch;

public record PrepareVideoBatchCommand(
    Guid ProductId,
    Guid ImageSetId,
    List<string> FileExtensions,
    bool ReplaceSet) : IRequest<Result<PrepareVideoBatchResult>>;

public record PrepareVideoBatchResult(Guid BatchId, List<PreparedVideoItem> Videos);
public record PreparedVideoItem(Guid VideoId, string FileName);

public class PrepareVideoBatchCommandHandler : IRequestHandler<PrepareVideoBatchCommand, Result<PrepareVideoBatchResult>>
{
    private readonly ICatalogDbContext _db;

    public PrepareVideoBatchCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<PrepareVideoBatchResult>> Handle(PrepareVideoBatchCommand request, CancellationToken ct)
    {
        var product = await _db.Products.FirstOrDefaultAsync(x => x.Id == request.ProductId, ct);
        if (product is null)
            return Result.Failure<PrepareVideoBatchResult>("Ürün bulunamadı.");

        var imageSet = await _db.ImageSets.FirstOrDefaultAsync(x => x.Id == request.ImageSetId, ct);
        if (imageSet is null)
            return Result.Failure<PrepareVideoBatchResult>("ImageSet bulunamadı.");

        var batchId = Guid.NewGuid();
        var batchShort = batchId.ToString("N")[..8];
        var preparedItems = new List<PreparedVideoItem>();

        for (int i = 0; i < request.FileExtensions.Count; i++)
        {
            var ext = request.FileExtensions[i].TrimStart('.').ToLowerInvariant();
            var fileName = $"{product.Code}_{imageSet.Code}_vid_{batchShort}_{(i + 1):D2}.{ext}";

            var video = new ProductVideo
            {
                Id = Guid.NewGuid(),
                ProductId = request.ProductId,
                ImageSetId = request.ImageSetId,
                FileName = fileName,
                SortOrder = i + 1,
                Status = ProductImageStatus.Pending,
                BatchId = batchId
            };

            _db.ProductVideos.Add(video);
            preparedItems.Add(new PreparedVideoItem(video.Id, fileName));
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success(new PrepareVideoBatchResult(batchId, preparedItems));
    }
}
