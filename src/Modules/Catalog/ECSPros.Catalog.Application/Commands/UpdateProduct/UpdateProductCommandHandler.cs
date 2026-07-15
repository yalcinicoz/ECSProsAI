using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.UpdateProduct;

public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, Result<bool>>
{
    private readonly ICatalogDbContext _context;

    public UpdateProductCommandHandler(ICatalogDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (product is null)
            return Result.Failure<bool>("Ürün bulunamadı.");

        var now = DateTime.UtcNow;

        if (product.BasePrice != request.BasePrice)
            _context.ProductPriceHistories.Add(new Domain.Entities.ProductPriceHistory
            {
                ProductId     = product.Id,
                PriceField    = "base_price",
                OldValue      = product.BasePrice,
                NewValue      = request.BasePrice,
                ChangedAt     = now,
                ChangedBy     = request.UpdatedBy,
                ChangedByName = request.UpdatedByName,
            });

        if (product.BaseCost != request.BaseCost)
            _context.ProductPriceHistories.Add(new Domain.Entities.ProductPriceHistory
            {
                ProductId     = product.Id,
                PriceField    = "base_cost",
                OldValue      = product.BaseCost,
                NewValue      = request.BaseCost,
                ChangedAt     = now,
                ChangedBy     = request.UpdatedBy,
                ChangedByName = request.UpdatedByName,
            });

        product.NameI18n             = request.NameI18n;
        product.ShortDescriptionI18n = request.ShortDescriptionI18n;
        product.DescriptionI18n      = request.DescriptionI18n;
        product.BasePrice            = request.BasePrice;
        product.BaseCost             = request.BaseCost;
        product.TaxRate              = request.TaxRate;
        product.IsSaleOpen           = request.IsActive;   // kontrat adı IsActive, anlamı satış anahtarı
        product.SupplierId           = request.SupplierId;
        product.SupplierProductCode  = request.SupplierProductCode;
        product.UpdatedBy = request.UpdatedBy;
        product.UpdatedAt = now;

        // Varyantsız üründe (tek özniteliksiz default varyant) fiyatın sahibi varyanttır —
        // kart fiyatı ve fiyat filtresi varyant BasePrice'ından okur. Ürün fiyatı değişince
        // default varyanta senkron yazılır; çok varyantlı üründe varyant fiyatına dokunulmaz.
        var varyantlar = await _context.ProductVariants
            .Where(v => v.ProductId == product.Id)
            .Select(v => new { Variant = v, AttrCount = v.VariantAttributes.Count })
            .ToListAsync(cancellationToken);

        if (varyantlar.Count == 1 && varyantlar[0].AttrCount == 0)
        {
            var defaultVaryant = varyantlar[0].Variant;

            if (defaultVaryant.BasePrice != request.BasePrice)
            {
                _context.VariantPriceHistories.Add(new Domain.Entities.VariantPriceHistory
                {
                    VariantId     = defaultVaryant.Id,
                    PriceType     = "base_price",
                    OldValue      = defaultVaryant.BasePrice,
                    NewValue      = request.BasePrice,
                    ChangedBy     = request.UpdatedBy,
                    ChangedByName = request.UpdatedByName,
                });
                defaultVaryant.BasePrice = request.BasePrice;
                defaultVaryant.UpdatedAt = now;
                defaultVaryant.UpdatedBy = request.UpdatedBy;
            }

            if (request.BaseCost.HasValue && defaultVaryant.BaseCost != request.BaseCost)
            {
                _context.VariantPriceHistories.Add(new Domain.Entities.VariantPriceHistory
                {
                    VariantId     = defaultVaryant.Id,
                    PriceType     = "base_cost",
                    OldValue      = defaultVaryant.BaseCost ?? 0,
                    NewValue      = request.BaseCost.Value,
                    ChangedBy     = request.UpdatedBy,
                    ChangedByName = request.UpdatedByName,
                });
                defaultVaryant.BaseCost  = request.BaseCost;
                defaultVaryant.UpdatedAt = now;
                defaultVaryant.UpdatedBy = request.UpdatedBy;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
