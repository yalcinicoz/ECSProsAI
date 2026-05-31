using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.AddProductVariants;

public record VariantAxisValueItem(Guid AttributeTypeId, Guid AttributeValueId);

public record AddProductVariantItem(
    string? Sku,
    List<VariantAxisValueItem> Attributes);

public record AddProductVariantsCommand(
    Guid ProductId,
    List<AddProductVariantItem> Variants
) : IRequest<Result<int>>;

public class AddProductVariantsCommandHandler : IRequestHandler<AddProductVariantsCommand, Result<int>>
{
    private readonly ICatalogDbContext _db;

    public AddProductVariantsCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<int>> Handle(AddProductVariantsCommand request, CancellationToken ct)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, ct);

        if (product is null)
            return Result.Failure<int>("Ürün bulunamadı.");

        // Collect attribute value codes for SKU auto-generation
        var valueIds = request.Variants
            .SelectMany(v => v.Attributes)
            .Select(a => a.AttributeValueId)
            .Distinct()
            .ToList();

        var valueCodeMap = await _db.AttributeValues
            .Where(v => valueIds.Contains(v.Id))
            .Select(v => new { v.Id, v.NameI18n })
            .ToDictionaryAsync(v => v.Id, v => v.NameI18n, ct);

        // Check for existing variant attribute combinations to avoid duplicates
        var existingVariants = await _db.ProductVariants
            .Include(v => v.VariantAttributes)
            .Where(v => v.ProductId == request.ProductId)
            .ToListAsync(ct);

        int added = 0;

        foreach (var item in request.Variants)
        {
            // Check duplicate combination
            var isDuplicate = existingVariants.Any(ev =>
            {
                if (ev.VariantAttributes.Count != item.Attributes.Count) return false;
                return item.Attributes.All(a =>
                    ev.VariantAttributes.Any(va =>
                        va.AttributeTypeId == a.AttributeTypeId &&
                        va.AttributeValueId == a.AttributeValueId));
            });

            if (isDuplicate) continue;

            // Auto-generate SKU if not provided
            string sku = item.Sku?.Trim() ?? GenerateSku(product.Code, item.Attributes, valueCodeMap);

            // Ensure SKU uniqueness
            var skuExists = await _db.ProductVariants.AnyAsync(v => v.Sku == sku, ct);
            if (skuExists) sku = $"{sku}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";

            var variant = new ProductVariant
            {
                Id        = Guid.NewGuid(),
                ProductId = request.ProductId,
                Sku       = sku,
                BasePrice = product.BasePrice,
                BaseCost  = product.BaseCost,
                IsActive  = true,
                CreatedAt = DateTime.UtcNow,
            };

            foreach (var attr in item.Attributes)
            {
                variant.VariantAttributes.Add(new ProductVariantAttribute
                {
                    Id              = Guid.NewGuid(),
                    VariantId       = variant.Id,
                    AttributeTypeId = attr.AttributeTypeId,
                    AttributeValueId = attr.AttributeValueId,
                    CreatedAt       = DateTime.UtcNow,
                });
            }

            _db.ProductVariants.Add(variant);
            existingVariants.Add(variant); // prevent in-loop duplicates
            added++;
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success(added);
    }

    private static string GenerateSku(
        string productCode,
        List<VariantAxisValueItem> attrs,
        Dictionary<Guid, Dictionary<string, string>> valueCodeMap)
    {
        var parts = attrs.Select(a =>
        {
            if (valueCodeMap.TryGetValue(a.AttributeValueId, out var nameI18n))
            {
                var name = nameI18n.GetValueOrDefault("tr")
                    ?? nameI18n.Values.FirstOrDefault()
                    ?? a.AttributeValueId.ToString("N")[..4];
                return name.Length > 6 ? name[..6].ToUpper() : name.ToUpper();
            }
            return a.AttributeValueId.ToString("N")[..4].ToUpper();
        });

        return $"{productCode}-{string.Join("-", parts)}";
    }
}
