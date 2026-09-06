using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.CreateProduct;

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Result<CreateProductResult>>
{
    private readonly ICatalogDbContext _context;

    public CreateProductCommandHandler(ICatalogDbContext context)
    {
        _context = context;
    }

    public async Task<Result<CreateProductResult>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var code = string.IsNullOrWhiteSpace(request.Code)
            ? $"PRD-{Guid.NewGuid().ToString("N")[..8].ToUpper()}"
            : request.Code.Trim();

        var exists = await _context.Products.AnyAsync(p => p.Code == code, cancellationToken);
        if (exists)
            return Result.Failure<CreateProductResult>($"'{code}' ürün kodu zaten mevcut.");

        var groupExists = await _context.ProductGroups.AnyAsync(g => g.Id == request.ProductGroupId, cancellationToken);
        if (!groupExists)
            return Result.Failure<CreateProductResult>("Ürün grubu bulunamadı.");

        var variants = request.Variants ?? [];

        if (variants.Count > 0)
        {
            var skus = variants.Select(v => v.Sku).ToList();
            var duplicateSku = await _context.ProductVariants.AnyAsync(v => skus.Contains(v.Sku), cancellationToken);
            if (duplicateSku)
                return Result.Failure<CreateProductResult>("Bir veya daha fazla SKU zaten kullanımda.");
        }

        var product = new Product
        {
            ProductGroupId       = request.ProductGroupId,
            Code                 = code,
            NameI18n             = request.NameI18n,
            ShortDescriptionI18n = request.ShortDescriptionI18n,
            DescriptionI18n      = request.DescriptionI18n,
            BasePrice            = request.BasePrice,
            BaseCost             = request.BaseCost,
            TaxRate              = request.TaxRate,
            IsSaleOpen           = false   // yeni ürün varsayılan satışa KAPALI (panelden açılır)
        };

        foreach (var v in variants)
        {
            product.Variants.Add(new ProductVariant
            {
                Sku       = v.Sku,
                BasePrice = v.BasePrice,
                BaseCost  = v.BaseCost,
                IsActive  = true
            });
        }

        // Satılabilir birim ProductVariant satırıdır (sepet/stok/sipariş VariantId'ye bağlı);
        // grubun varyant ekseni yoksa ürün özniteliksiz tek "default" varyantla doğar —
        // "her ürünün en az 1 varyantı vardır" kuralı UI'a değil buraya bağlıdır.
        // Eksenli grupta 0 varyant = taslak: varyantlar kombinasyon seçilerek sonradan açılır.
        if (variants.Count == 0)
        {
            var grupEksenli = await _context.ProductGroupAttributes
                .AnyAsync(a => a.ProductGroupId == request.ProductGroupId && a.IsVariant, cancellationToken);

            if (!grupEksenli)
            {
                var sku = code;
                if (await _context.ProductVariants.AnyAsync(v => v.Sku == sku, cancellationToken))
                    sku = $"{sku}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";

                product.Variants.Add(new ProductVariant
                {
                    Sku       = sku,
                    BasePrice = request.BasePrice,
                    BaseCost  = request.BaseCost,
                    IsActive  = true
                });
            }
        }

        // Grup varsayılan özellik değerleri (2026-09-06): seçim tipli özelliklerde grubun
        // DefaultAttributeValueId'si yeni ürüne otomatik yazılır (örn. "Ürün Grubu" = Ceket).
        var varsayilanlar = await _context.ProductGroupAttributes
            .Where(a => a.ProductGroupId == request.ProductGroupId && !a.IsVariant && a.DefaultAttributeValueId != null)
            .Select(a => new { a.AttributeTypeId, ValueId = a.DefaultAttributeValueId!.Value })
            .ToListAsync(cancellationToken);
        foreach (var v in varsayilanlar)
            product.Attributes.Add(new ProductAttribute
            {
                Id = Guid.NewGuid(), AttributeTypeId = v.AttributeTypeId, AttributeValueId = v.ValueId, CreatedAt = DateTime.UtcNow
            });

        _context.Products.Add(product);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(new CreateProductResult(product.Id, product.Code));
    }
}
