using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.SetFirmPlatformVariantPrice;

public record SetFirmPlatformVariantPriceCommand(
    Guid FirmPlatformId,
    Guid VariantId,
    string? PriceType,
    decimal? PriceMultiplier,
    decimal? Price,
    decimal? CompareAtPrice,
    bool IsActive
) : IRequest<Result<Guid>>;

public class SetFirmPlatformVariantPriceCommandHandler : IRequestHandler<SetFirmPlatformVariantPriceCommand, Result<Guid>>
{
    private readonly ICatalogDbContext _db;

    public SetFirmPlatformVariantPriceCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(SetFirmPlatformVariantPriceCommand request, CancellationToken ct)
    {
        var existing = await _db.FirmPlatformVariants
            .FirstOrDefaultAsync(fpv => fpv.FirmPlatformId == request.FirmPlatformId && fpv.VariantId == request.VariantId, ct);

        if (existing is not null)
        {
            existing.PriceType = request.PriceType;
            existing.PriceMultiplier = request.PriceMultiplier;
            existing.Price = request.Price;
            existing.CompareAtPrice = request.CompareAtPrice;
            existing.IsActive = request.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return Result.Success(existing.Id);
        }

        var variantExists = await _db.ProductVariants.AnyAsync(v => v.Id == request.VariantId, ct);
        if (!variantExists)
            return Result.Failure<Guid>("Varyant bulunamadı.");

        var fpv = new FirmPlatformVariant
        {
            Id = Guid.NewGuid(),
            FirmPlatformId = request.FirmPlatformId,
            VariantId = request.VariantId,
            PriceType = request.PriceType,
            PriceMultiplier = request.PriceMultiplier,
            Price = request.Price,
            CompareAtPrice = request.CompareAtPrice,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _db.FirmPlatformVariants.Add(fpv);
        await _db.SaveChangesAsync(ct);

        return Result.Success(fpv.Id);
    }
}
