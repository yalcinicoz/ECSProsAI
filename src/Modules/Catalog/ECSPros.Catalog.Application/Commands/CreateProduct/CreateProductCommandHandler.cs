using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.CreateProduct;

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Result<Guid>>
{
    private readonly ICatalogDbContext _context;

    public CreateProductCommandHandler(ICatalogDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var exists = await _context.Products.AnyAsync(p => p.Code == request.Code, cancellationToken);
        if (exists)
            return Result.Failure<Guid>($"'{request.Code}' ürün kodu zaten mevcut.");

        var groupExists = await _context.ProductGroups.AnyAsync(g => g.Id == request.ProductGroupId, cancellationToken);
        if (!groupExists)
            return Result.Failure<Guid>("Ürün grubu bulunamadı.");

        if (request.Variants.Count == 0)
            return Result.Failure<Guid>("En az bir varyant gereklidir.");

        // SKU benzersizlik kontrolü
        var skus = request.Variants.Select(v => v.Sku).ToList();
        var duplicateSku = await _context.ProductVariants.AnyAsync(v => skus.Contains(v.Sku), cancellationToken);
        if (duplicateSku)
            return Result.Failure<Guid>("Bir veya daha fazla SKU zaten kullanımda.");

        var product = new Product
        {
            ProductGroupId = request.ProductGroupId,
            Code = request.Code,
            NameI18n = request.NameI18n,
            ShortDescriptionI18n = request.ShortDescriptionI18n,
            IsActive = true
        };

        foreach (var v in request.Variants)
        {
            product.Variants.Add(new ProductVariant
            {
                Sku = v.Sku,
                BasePrice = v.BasePrice,
                BaseCost = v.BaseCost,
                IsActive = true
            });
        }

        _context.Products.Add(product);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(product.Id);
    }
}
