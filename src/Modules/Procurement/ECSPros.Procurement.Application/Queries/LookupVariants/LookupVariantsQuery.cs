using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Queries.LookupVariants;

/// <summary>
/// T4 ayrıştırma araması (barkod okuyucu dostu): önce varyant barkodu TAM eşleşmesi, sonra SKU tam,
/// sonra SKU/ürün kodu/ad İÇEREN arama (en çok 10 aday). K9: yalnız MEVCUT kartlar.
/// </summary>
public record LookupVariantsQuery(string Term) : IRequest<Result<List<VariantLookupDto>>>;

public record VariantLookupDto(
    Guid VariantId, Guid ProductId, string ProductCode, string Name,
    string Sku, string? Barcode, string? Color, string? Size, decimal Price, bool Exact);

public class LookupVariantsQueryHandler(ICatalogDbContext catDb)
    : IRequestHandler<LookupVariantsQuery, Result<List<VariantLookupDto>>>
{
    public async Task<Result<List<VariantLookupDto>>> Handle(LookupVariantsQuery request, CancellationToken ct)
    {
        var term = (request.Term ?? "").Trim();
        if (term.Length < 2) return Result.Success(new List<VariantLookupDto>());
        var lower = term.ToLower();

        var q = catDb.ProductVariants.AsNoTracking()
            .Where(v => v.Barcode == term || v.Sku == term);
        var exact = await Project(q, catDb, true).Take(10).ToListAsync(ct);
        if (exact.Count > 0) return Result.Success(exact);

        var q2 = catDb.ProductVariants.AsNoTracking()
            .Where(v => v.Sku.ToLower().Contains(lower)
                || (v.Barcode ?? "").Contains(term));
        var partial = await Project(q2, catDb, false).Take(10).ToListAsync(ct);
        if (partial.Count > 0) return Result.Success(partial);

        // ürün kodu / ad üzerinden (ürünün ilk aktif varyantları)
        var q3 = catDb.ProductVariants.AsNoTracking()
            .Where(v => catDb.Products.Any(p => p.Id == v.ProductId
                && (p.Code.ToLower().Contains(lower)
                    || ECSPros.Catalog.Application.Helpers.PgJsonFunctions.JsonText(p.NameI18n, "tr")!.ToLower().Contains(lower))));
        var byProduct = await Project(q3, catDb, false).Take(10).ToListAsync(ct);
        return Result.Success(byProduct);
    }

    private static IQueryable<VariantLookupDto> Project(IQueryable<ECSPros.Catalog.Domain.Entities.ProductVariant> q, ICatalogDbContext db, bool exact)
        => q.OrderBy(v => v.Sku).Select(v => new VariantLookupDto(
            v.Id, v.ProductId,
            db.Products.Where(p => p.Id == v.ProductId).Select(p => p.Code).FirstOrDefault() ?? "",
            db.Products.Where(p => p.Id == v.ProductId).Select(p => ECSPros.Catalog.Application.Helpers.PgJsonFunctions.JsonText(p.NameI18n, "tr")).FirstOrDefault() ?? "",
            v.Sku, v.Barcode,
            db.ProductVariantAttributes
                .Where(a => a.VariantId == v.Id && db.AttributeTypes.Any(t => t.Id == a.AttributeTypeId && t.Code == "renk"))
                .Select(a => db.AttributeValues.Where(av => av.Id == a.AttributeValueId)
                    .Select(av => ECSPros.Catalog.Application.Helpers.PgJsonFunctions.JsonText(av.NameI18n, "tr")).FirstOrDefault())
                .FirstOrDefault(),
            db.ProductVariantAttributes
                .Where(a => a.VariantId == v.Id && db.AttributeTypes.Any(t => t.Id == a.AttributeTypeId && t.Code == "beden"))
                .Select(a => db.AttributeValues.Where(av => av.Id == a.AttributeValueId)
                    .Select(av => ECSPros.Catalog.Application.Helpers.PgJsonFunctions.JsonText(av.NameI18n, "tr")).FirstOrDefault())
                .FirstOrDefault(),
            v.BasePrice > 0 ? v.BasePrice
                : db.Products.Where(p => p.Id == v.ProductId).Select(p => p.BasePrice).FirstOrDefault(),
            exact));
}
