using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.ArchiveProductImage;

public record ArchiveProductImageCommand(Guid ImageId) : IRequest<Result<bool>>;

public class ArchiveProductImageCommandHandler : IRequestHandler<ArchiveProductImageCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public ArchiveProductImageCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(ArchiveProductImageCommand request, CancellationToken ct)
    {
        var image = await _db.ProductImages
            .FirstOrDefaultAsync(x => x.Id == request.ImageId, ct);

        if (image is null)
            return Result.Failure<bool>("Resim bulunamadı.");

        image.Status = ProductImageStatus.Archived;
        image.ArchivedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
