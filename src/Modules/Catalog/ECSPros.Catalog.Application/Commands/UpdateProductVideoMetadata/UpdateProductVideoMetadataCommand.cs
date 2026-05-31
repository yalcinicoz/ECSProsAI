using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.UpdateProductVideoMetadata;

public record UpdateProductVideoMetadataCommand(
    Guid VideoId,
    int SortOrder,
    string? ThumbnailFileName) : IRequest<Result<bool>>;

public class UpdateProductVideoMetadataCommandHandler : IRequestHandler<UpdateProductVideoMetadataCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public UpdateProductVideoMetadataCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(UpdateProductVideoMetadataCommand request, CancellationToken ct)
    {
        var video = await _db.ProductVideos.FirstOrDefaultAsync(x => x.Id == request.VideoId, ct);
        if (video is null)
            return Result.Failure<bool>("Video bulunamadı.");

        video.SortOrder = request.SortOrder;
        video.ThumbnailFileName = request.ThumbnailFileName;

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
