using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetProductsByAttributeValue;

public record GetProductsByAttributeValueQuery(Guid AttributeValueId) : IRequest<Result<List<ProductByValueDto>>>;

public record ProductByValueDto(Guid Id, string Code, Dictionary<string, string> NameI18n, bool IsActive, string UsageType);

public class GetProductsByAttributeValueQueryHandler
    : IRequestHandler<GetProductsByAttributeValueQuery, Result<List<ProductByValueDto>>>
{
    private readonly ICatalogDbContext _db;

    public GetProductsByAttributeValueQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<List<ProductByValueDto>>> Handle(
        GetProductsByAttributeValueQuery request, CancellationToken ct)
    {
        // Ürün seviyesinde doğrudan kullanım
        var direct = await _db.ProductAttributes
            .Where(pa => pa.AttributeValueId == request.AttributeValueId)
            .Select(pa => new { pa.Product.Id, pa.Product.Code, pa.Product.NameI18n, pa.Product.IsActive })
            .Distinct()
            .ToListAsync(ct);

        // Varyant üzerinden kullanım
        var viaVariant = await _db.ProductVariantAttributes
            .Where(pva => pva.AttributeValueId == request.AttributeValueId)
            .Select(pva => new { pva.Variant.Product.Id, pva.Variant.Product.Code, pva.Variant.Product.NameI18n, pva.Variant.Product.IsActive })
            .Distinct()
            .ToListAsync(ct);

        var directIds = direct.Select(p => p.Id).ToHashSet();

        var result = direct
            .Select(p => new ProductByValueDto(p.Id, p.Code, p.NameI18n, p.IsActive, "direct"))
            .Concat(viaVariant
                .Where(p => !directIds.Contains(p.Id))
                .Select(p => new ProductByValueDto(p.Id, p.Code, p.NameI18n, p.IsActive, "variant")))
            .OrderBy(p => p.Code)
            .ToList();

        return Result.Success(result);
    }
}
