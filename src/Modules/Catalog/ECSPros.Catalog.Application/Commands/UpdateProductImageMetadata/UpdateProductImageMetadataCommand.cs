using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.UpdateProductImageMetadata;

public record UpdateProductImageMetadataCommand(
    Guid ImageId,
    int SortOrder,
    bool IsProductCover,
    bool IsVariantCover) : IRequest<Result<bool>>;

public class UpdateProductImageMetadataCommandHandler
    : IRequestHandler<UpdateProductImageMetadataCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public UpdateProductImageMetadataCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(UpdateProductImageMetadataCommand request, CancellationToken ct)
    {
        var image = await _db.ProductImages
            .FirstOrDefaultAsync(x => x.Id == request.ImageId, ct);

        if (image is null)
            return Result.Failure<bool>("Resim bulunamadı.");

        image.SortOrder = request.SortOrder;
        image.IsProductCover = request.IsProductCover;
        image.IsVariantCover = request.IsVariantCover;

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
