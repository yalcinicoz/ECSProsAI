using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.SetChannelVariantPrice;

public record SetChannelVariantPriceCommand(
    Guid FirmPlatformId,
    Guid VariantId,
    string? PriceType,
    decimal? PriceMultiplier,
    decimal? Price,
    decimal? CompareAtPrice,
    bool IsActive,
    Guid? ChangedBy = null,
    string? ChangedByName = null,
    string? FirmPlatformCode = null
) : IRequest<Result<Guid>>;

public class SetChannelVariantPriceCommandHandler : IRequestHandler<SetChannelVariantPriceCommand, Result<Guid>>
{
    private readonly IStorefrontDbContext _sfDb;
    private readonly ICatalogDbContext _catDb;

    public SetChannelVariantPriceCommandHandler(IStorefrontDbContext sfDb, ICatalogDbContext catDb)
    {
        _sfDb = sfDb;
        _catDb = catDb;
    }

    public async Task<Result<Guid>> Handle(SetChannelVariantPriceCommand request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var existing = await _sfDb.ChannelVariants
            .FirstOrDefaultAsync(cv => cv.FirmPlatformId == request.FirmPlatformId && cv.VariantId == request.VariantId, ct);

        if (existing is not null)
        {
            var oldPrice = existing.Price;
            var oldMultiplier = existing.PriceMultiplier;
            var oldType = existing.PriceType;

            existing.PriceType = request.PriceType;
            existing.PriceMultiplier = request.PriceMultiplier;
            existing.Price = request.Price;
            existing.CompareAtPrice = request.CompareAtPrice;
            existing.IsActive = request.IsActive;
            existing.UpdatedAt = now;

            // record history if effective price changed
            var oldVal = oldType == "multiplier" ? oldMultiplier : oldPrice;
            var newVal = request.PriceType == "multiplier" ? request.PriceMultiplier : request.Price;
            if (oldVal != newVal || oldType != request.PriceType)
                _catDb.VariantPriceHistories.Add(new VariantPriceHistory
                {
                    VariantId       = request.VariantId,
                    FirmPlatformId  = request.FirmPlatformId,
                    FirmPlatformCode = request.FirmPlatformCode,
                    PriceType       = "platform_price",
                    OldValue        = oldVal ?? 0,
                    NewValue        = newVal ?? 0,
                    ChangedAt       = now,
                    ChangedBy       = request.ChangedBy ?? Guid.Empty,
                    ChangedByName   = request.ChangedByName,
                });

            await _catDb.SaveChangesAsync(ct);
            await _sfDb.SaveChangesAsync(ct);
            return Result.Success(existing.Id);
        }

        var variantExists = await _catDb.ProductVariants.AnyAsync(v => v.Id == request.VariantId, ct);
        if (!variantExists)
            return Result.Failure<Guid>("Varyant bulunamadı.");

        var cv2 = new ChannelVariant
        {
            Id             = Guid.NewGuid(),
            FirmPlatformId = request.FirmPlatformId,
            VariantId      = request.VariantId,
            PriceType      = request.PriceType,
            PriceMultiplier = request.PriceMultiplier,
            Price          = request.Price,
            CompareAtPrice = request.CompareAtPrice,
            IsActive       = request.IsActive,
            CreatedAt      = now,
        };
        _sfDb.ChannelVariants.Add(cv2);

        var newVal2 = request.PriceType == "multiplier" ? request.PriceMultiplier : request.Price;
        _catDb.VariantPriceHistories.Add(new VariantPriceHistory
        {
            VariantId        = request.VariantId,
            FirmPlatformId   = request.FirmPlatformId,
            FirmPlatformCode = request.FirmPlatformCode,
            PriceType        = "platform_price",
            OldValue         = 0,
            NewValue         = newVal2 ?? 0,
            ChangedAt        = now,
            ChangedBy        = request.ChangedBy ?? Guid.Empty,
            ChangedByName    = request.ChangedByName,
        });

        await _catDb.SaveChangesAsync(ct);
        await _sfDb.SaveChangesAsync(ct);
        return Result.Success(cv2.Id);
    }
}
