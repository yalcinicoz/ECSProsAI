using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.ConfirmVideoBatch;

public record ConfirmVideoBatchItem(Guid VideoId, int SortOrder, string? ThumbnailFileName);

public record ConfirmVideoBatchCommand(
    Guid ProductId,
    Guid BatchId,
    bool ReplaceSet,
    List<ConfirmVideoBatchItem> ConfirmedVideos) : IRequest<Result<int>>;

public class ConfirmVideoBatchCommandHandler : IRequestHandler<ConfirmVideoBatchCommand, Result<int>>
{
    private readonly ICatalogDbContext _db;

    public ConfirmVideoBatchCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<int>> Handle(ConfirmVideoBatchCommand request, CancellationToken ct)
    {
        var batchVideos = await _db.ProductVideos
            .Where(x => x.BatchId == request.BatchId && x.ProductId == request.ProductId)
            .ToListAsync(ct);

        if (!batchVideos.Any())
            return Result.Failure<int>("Batch bulunamadı.");

        var confirmedIds = request.ConfirmedVideos.Select(x => x.VideoId).ToHashSet();
        var imageSetId = batchVideos.First().ImageSetId;

        if (request.ReplaceSet)
        {
            var existing = await _db.ProductVideos
                .Where(x => x.ProductId == request.ProductId
                    && x.ImageSetId == imageSetId
                    && x.Status == ProductImageStatus.Active
                    && x.BatchId != request.BatchId)
                .ToListAsync(ct);

            foreach (var v in existing)
            {
                v.Status = ProductImageStatus.Archived;
                v.ArchivedAt = DateTime.UtcNow;
            }
        }

        int activated = 0;
        foreach (var video in batchVideos)
        {
            if (confirmedIds.Contains(video.Id))
            {
                var meta = request.ConfirmedVideos.First(x => x.VideoId == video.Id);
                video.Status = ProductImageStatus.Active;
                video.SortOrder = meta.SortOrder;
                video.ThumbnailFileName = meta.ThumbnailFileName;
                activated++;
            }
            else
            {
                video.Status = ProductImageStatus.Cancelled;
            }
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success(activated);
    }
}
