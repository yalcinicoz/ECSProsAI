using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.UpdateImageSet;

public record UpdateImageSetCommand(
    Guid Id,
    string Name,
    bool IsDefault,
    Guid? FallbackSetId,
    int SortPriority,
    bool IsActive) : IRequest<Result<bool>>;

public class UpdateImageSetCommandHandler : IRequestHandler<UpdateImageSetCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public UpdateImageSetCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(UpdateImageSetCommand request, CancellationToken ct)
    {
        var imageSet = await _db.ImageSets.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (imageSet is null)
            return Result.Failure<bool>("ImageSet bulunamadı.");

        imageSet.Name = request.Name;
        imageSet.IsDefault = request.IsDefault;
        imageSet.FallbackSetId = request.FallbackSetId;
        imageSet.SortPriority = request.SortPriority;
        imageSet.IsActive = request.IsActive;

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
