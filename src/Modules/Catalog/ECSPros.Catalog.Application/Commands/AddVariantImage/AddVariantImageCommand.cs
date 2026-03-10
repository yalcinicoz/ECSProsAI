using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.AddVariantImage;

public record AddVariantImageCommand(
    Guid VariantId,
    string ImageUrl,
    bool IsMain,
    int SortOrder
) : IRequest<Result<Guid>>;

public class AddVariantImageCommandHandler : IRequestHandler<AddVariantImageCommand, Result<Guid>>
{
    private readonly ICatalogDbContext _db;

    public AddVariantImageCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(AddVariantImageCommand request, CancellationToken ct)
    {
        var variantExists = await _db.ProductVariants.AnyAsync(v => v.Id == request.VariantId, ct);
        if (!variantExists)
            return Result.Failure<Guid>("Varyant bulunamadı.");

        if (request.IsMain)
        {
            // Mevcut ana görseli kaldır
            var existing = await _db.ProductVariantImages
                .Where(i => i.VariantId == request.VariantId && i.IsMain)
                .ToListAsync(ct);
            foreach (var img in existing) img.IsMain = false;
        }

        var image = new ProductVariantImage
        {
            Id = Guid.NewGuid(),
            VariantId = request.VariantId,
            ImageUrl = request.ImageUrl,
            IsMain = request.IsMain,
            SortOrder = request.SortOrder,
            CreatedAt = DateTime.UtcNow
        };

        _db.ProductVariantImages.Add(image);
        await _db.SaveChangesAsync(ct);

        return Result.Success(image.Id);
    }
}
