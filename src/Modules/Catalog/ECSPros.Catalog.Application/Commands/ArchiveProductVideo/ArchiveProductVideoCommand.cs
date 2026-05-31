using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.ArchiveProductVideo;

public record ArchiveProductVideoCommand(Guid VideoId) : IRequest<Result<bool>>;

public class ArchiveProductVideoCommandHandler : IRequestHandler<ArchiveProductVideoCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public ArchiveProductVideoCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(ArchiveProductVideoCommand request, CancellationToken ct)
    {
        var video = await _db.ProductVideos.FirstOrDefaultAsync(x => x.Id == request.VideoId, ct);
        if (video is null)
            return Result.Failure<bool>("Video bulunamadı.");

        video.Status = ProductImageStatus.Archived;
        video.ArchivedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
