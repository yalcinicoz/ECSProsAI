using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.RestoreVideoBatch;

public record RestoreVideoBatchCommand(Guid ProductId, Guid BatchId) : IRequest<Result<int>>;

public class RestoreVideoBatchCommandHandler : IRequestHandler<RestoreVideoBatchCommand, Result<int>>
{
    private readonly ICatalogDbContext _db;

    public RestoreVideoBatchCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<int>> Handle(RestoreVideoBatchCommand request, CancellationToken ct)
    {
        var batchVideos = await _db.ProductVideos
            .Where(x => x.BatchId == request.BatchId
                && x.ProductId == request.ProductId
                && x.Status == ProductImageStatus.Archived)
            .ToListAsync(ct);

        if (!batchVideos.Any())
            return Result.Failure<int>("Arşiv batch'i bulunamadı.");

        var imageSetId = batchVideos.First().ImageSetId;

        var currentActive = await _db.ProductVideos
            .Where(x => x.ProductId == request.ProductId
                && x.ImageSetId == imageSetId
                && x.Status == ProductImageStatus.Active)
            .ToListAsync(ct);

        foreach (var v in currentActive)
        {
            v.Status = ProductImageStatus.Archived;
            v.ArchivedAt = DateTime.UtcNow;
        }

        foreach (var v in batchVideos)
        {
            v.Status = ProductImageStatus.Active;
            v.ArchivedAt = null;
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success(batchVideos.Count);
    }
}
