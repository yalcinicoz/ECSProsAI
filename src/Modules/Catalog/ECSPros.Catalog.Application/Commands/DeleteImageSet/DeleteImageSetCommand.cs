using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.DeleteImageSet;

public record DeleteImageSetCommand(Guid Id) : IRequest<Result<bool>>;

public class DeleteImageSetCommandHandler : IRequestHandler<DeleteImageSetCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public DeleteImageSetCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(DeleteImageSetCommand request, CancellationToken ct)
    {
        var set = await _db.ImageSets.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (set is null)
            return Result.Failure<bool>("Resim seti bulunamadı.");

        if (set.IsDefault)
            return Result.Failure<bool>("Varsayılan resim seti silinemez.");

        var hasImages = await _db.ProductImages.AnyAsync(x => x.ImageSetId == request.Id, ct);
        if (hasImages)
            return Result.Failure<bool>("Bu resim setine bağlı resimler var, silinemez.");

        var hasVideos = await _db.ProductVideos.AnyAsync(x => x.ImageSetId == request.Id, ct);
        if (hasVideos)
            return Result.Failure<bool>("Bu resim setine bağlı videolar var, silinemez.");

        var hasMappings = await _db.ProductImageSetMappings
            .AnyAsync(x => x.ForSetId == request.Id || x.UseSetId == request.Id, ct);
        if (hasMappings)
            return Result.Failure<bool>("Bu resim seti ürün eşleşmelerinde kullanılıyor, silinemez.");

        var usedAsFallback = await _db.ImageSets.AnyAsync(x => x.FallbackSetId == request.Id, ct);
        if (usedAsFallback)
            return Result.Failure<bool>("Bu resim seti başka bir setin fallback'i olarak tanımlı, silinemez.");

        set.IsDeleted = true;
        set.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Result.Success(true);
    }
}
