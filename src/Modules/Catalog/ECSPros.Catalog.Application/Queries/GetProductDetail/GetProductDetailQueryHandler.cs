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
            .Include(p => p.Variants)
                .ThenInclude(v => v.Images)
            .FirstOrDefaultAsync(p => p.Code == request.Code, cancellationToken);

        if (product is null)
            return Result.Failure<ProductDetailDto>($"Ürün '{request.Code}' bulunamadı.");

        var dto = new ProductDetailDto(
            product.Id,
            product.Code,
            product.NameI18n,
            product.ShortDescriptionI18n,
            product.ProductGroupId,
            product.IsActive,
            product.Variants.Select(v => new VariantDto(
                v.Id,
                v.Sku,
                v.BasePrice,
                v.BaseCost,
                v.IsActive,
                v.Images.OrderBy(i => i.SortOrder)
                    .Select(i => new VariantImageDto(i.Id, i.ImageUrl, i.SortOrder, i.IsMain))
                    .ToList()
            )).ToList()
        );

        return Result.Success(dto);
    }
}
