using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetProductDetail;

public class GetProductDetailQueryHandler : IRequestHandler<GetProductDetailQuery, Result<ProductDetailDto>>
{
    private readonly ICatalogDbContext _context;

    public GetProductDetailQueryHandler(ICatalogDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ProductDetailDto>> Handle(GetProductDetailQuery request, CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .Include(p => p.Attributes)
                .ThenInclude(a => a.AttributeType)
            .Include(p => p.Attributes)
                .ThenInclude(a => a.AttributeValue)
            .Include(p => p.Variants)
                .ThenInclude(v => v.VariantAttributes)
                    .ThenInclude(va => va.AttributeType)
            .Include(p => p.Variants)
                .ThenInclude(v => v.VariantAttributes)
                    .ThenInclude(va => va.AttributeValue)
            .Include(p => p.Variants)
                .ThenInclude(v => v.Images)
            .FirstOrDefaultAsync(p => p.Code == request.Code, cancellationToken);

        if (product is null)
            return Result.Failure<ProductDetailDto>($"Ürün '{request.Code}' bulunamadı.");

        // Ürün grubunun eksen alt özellik şeması
        var axisSubAttrSchema = await _context.ProductGroupAxisSubAttributes
            .Where(s => s.ProductGroupId == product.ProductGroupId)
            .Include(s => s.AxisAttributeType)
            .Include(s => s.SubAttributeType)
            .OrderBy(s => s.AxisAttributeType.SortOrder)
            .ThenBy(s => s.SortOrder)
            .ToListAsync(cancellationToken);

        var schemaGrouped = axisSubAttrSchema
            .GroupBy(s => s.AxisAttributeTypeId)
            .Select(g => new AxisSubAttributeSchemaDto(
                g.Key,
                g.First().AxisAttributeType.Code,
                g.First().AxisAttributeType.NameI18n,
                g.Select(s => new SubAttributeSchemaItemDto(
                    s.SubAttributeTypeId,
                    s.SubAttributeType.Code,
                    s.SubAttributeType.NameI18n,
                    s.IsRequired)).ToList()))
            .ToList();

        // Bu ürüne ait eksen alt özellik değerleri
        var axisSubAttrValues = await _context.ProductAxisSubAttributeValues
            .Where(v => v.ProductId == product.Id)
            .Select(v => new ProductAxisSubAttributeValueDto(v.AttributeValueId, v.SubAttributeTypeId, v.Value))
            .ToListAsync(cancellationToken);

        var dto = new ProductDetailDto(
            product.Id,
            product.Code,
            product.NameI18n,
            product.ShortDescriptionI18n,
            product.DescriptionI18n,
            product.ProductGroupId,
            product.IsActive,
            product.BasePrice,
            product.BaseCost,
            product.TaxRate,
            product.SupplierId,
            product.SupplierProductCode,
            product.CreatedAt,
            product.UpdatedAt,
            product.Attributes
                .OrderBy(a => a.AttributeType.SortOrder)
                .Select(a => new ProductAttributeDto(
                    a.Id,
                    a.AttributeTypeId,
                    a.AttributeType.Code,
                    a.AttributeType.NameI18n,
                    a.AttributeValueId,
                    a.AttributeValue?.NameI18n,
                    a.CustomValue != null ? System.Text.Json.JsonSerializer.Serialize(a.CustomValue) : null))
                .ToList(),
            product.Variants
                .OrderBy(v => v.Id)
                .Select(v => new VariantDto(
                    v.Id,
                    v.Sku,
                    v.Barcode,
                    v.BasePrice,
                    v.BaseCost,
                    v.IsActive,
                    v.VariantAttributes
                        .OrderBy(va => va.AttributeType.SortOrder)
                        .Select(va => new VariantAttributeDto(
                            va.AttributeTypeId,
                            va.AttributeType.Code,
                            va.AttributeType.NameI18n,
                            va.AttributeValueId,
                            va.AttributeValue.NameI18n))
                        .ToList(),
                    v.Images.OrderBy(i => i.SortOrder)
                        .Select(i => new VariantImageDto(i.Id, i.ImageUrl, i.SortOrder, i.IsMain))
                        .ToList()
                )).ToList(),
            product.Tags,
            product.Slug,
            product.MetaTitleI18n,
            product.MetaDescriptionI18n,
            product.MetaKeywordsI18n,
            schemaGrouped,
            axisSubAttrValues
        );

        return Result.Success(dto);
    }
}
