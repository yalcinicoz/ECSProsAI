using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.UpdateVariantPrice;

public record UpdateVariantPriceCommand(
    Guid VariantId,
    decimal? BasePrice,
    decimal? BaseCost,
    Guid ChangedBy,
    string? ChangedByName
) : IRequest<Result<bool>>;

public class UpdateVariantPriceCommandHandler : IRequestHandler<UpdateVariantPriceCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public UpdateVariantPriceCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(UpdateVariantPriceCommand request, CancellationToken ct)
    {
        var variant = await _db.ProductVariants
            .FirstOrDefaultAsync(v => v.Id == request.VariantId, ct);

        if (variant is null)
            return Result.Failure<bool>("Varyant bulunamadı.");

        if (request.BasePrice.HasValue && request.BasePrice.Value != variant.BasePrice)
        {
            _db.VariantPriceHistories.Add(new VariantPriceHistory
            {
                VariantId = variant.Id,
                PriceType = "base_price",
                OldValue = variant.BasePrice,
                NewValue = request.BasePrice.Value,
                ChangedBy = request.ChangedBy,
                ChangedByName = request.ChangedByName,
            });
            variant.BasePrice = request.BasePrice.Value;
        }

        if (request.BaseCost.HasValue && request.BaseCost.Value != (variant.BaseCost ?? 0))
        {
            _db.VariantPriceHistories.Add(new VariantPriceHistory
            {
                VariantId = variant.Id,
                PriceType = "base_cost",
                OldValue = variant.BaseCost ?? 0,
                NewValue = request.BaseCost.Value,
                ChangedBy = request.ChangedBy,
                ChangedByName = request.ChangedByName,
            });
            variant.BaseCost = request.BaseCost.Value;
        }

        variant.UpdatedAt = DateTime.UtcNow;
        variant.UpdatedBy = request.ChangedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
