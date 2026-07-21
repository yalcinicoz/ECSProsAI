using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetSupplierProductVariants;

/// <summary>Partner owner-scoped: tedarikçinin (SupplierId) kendi ürününü (SupplierProductCode) ve
/// varyantlarını (sku→variantId) çözer. Canlı + sahipli değilse başarısız — PUT /stock için.</summary>
public record GetSupplierProductVariantsQuery(Guid SupplierId, string SupplierProductCode)
    : IRequest<Result<SupplierProductVariantsDto>>;

public record SupplierProductVariantsDto(Guid ProductId, string ProductCode, List<SupplierVariantRef> Variants);
public record SupplierVariantRef(string Sku, Guid VariantId);

public class GetSupplierProductVariantsQueryHandler
    : IRequestHandler<GetSupplierProductVariantsQuery, Result<SupplierProductVariantsDto>>
{
    private readonly ICatalogDbContext _db;
    public GetSupplierProductVariantsQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<SupplierProductVariantsDto>> Handle(GetSupplierProductVariantsQuery request, CancellationToken ct)
    {
        var product = await _db.Products
            .Include(p => p.Variants.Where(v => !v.IsDeleted))
            .FirstOrDefaultAsync(p => p.SupplierId == request.SupplierId
                && p.SupplierProductCode == request.SupplierProductCode, ct);

        if (product is null)
            return Result.Failure<SupplierProductVariantsDto>(
                $"'{request.SupplierProductCode}' kodlu ürününüz bulunamadı (henüz onaylanmamış olabilir).");

        var variants = product.Variants.Select(v => new SupplierVariantRef(v.Sku, v.Id)).ToList();
        return Result.Success(new SupplierProductVariantsDto(product.Id, product.Code, variants));
    }
}
