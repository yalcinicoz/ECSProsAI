using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetProducts;

/// <summary>
/// Ürünler (admin katalog listesi) DataGrid şeması (plan F4): beyaz listeli sıralama/filtre + mevcut adlandırılmış filtreler
/// tek yerden (liste ve export AYNI filtre modeli). NameI18n jsonb sıralanmaz (yalnız global aramada tr metni).
/// </summary>
public static class ProductGrid
{
    public static readonly string[] SourceTypes = { "own", "seller", "supply" };

    public static readonly GridSchema<Product> Schema = new GridSchema<Product>()
        .Text("code", p => p.Code)
        .Text("supplierProductCode", p => p.SupplierProductCode)
        .Text("slug", p => p.Slug)
        .Enum("sourceType", p => p.SourceType, SourceTypes)
        .Bool("isSaleOpen", p => p.IsSaleOpen)
        .Number("basePrice", p => p.BasePrice)
        .Number("taxRate", p => p.TaxRate)
        .Date("createdAt", p => p.CreatedAt)
        .Guid("productGroupId", p => p.ProductGroupId)
        .Guid("supplierId", p => p.SupplierId)
        .Sort("code", p => p.Code)
        .Sort("createdAt", p => p.CreatedAt)
        .Sort("basePrice", p => p.BasePrice)
        .Sort("isSaleOpen", p => p.IsSaleOpen)
        .Sort("sourceType", p => p.SourceType)
        .DefaultSort(p => p.CreatedAt, desc: true)
        .TieBreaker(p => p.Id);

    /// <summary>Mevcut adlandırılmış filtreler (activeOnly, productGroupId) + global arama (kod ya da tr ad).</summary>
    public static IQueryable<Product> ApplyNamed(IQueryable<Product> query, ProductListFilters f)
    {
        if (f.ActiveOnly) query = query.Where(x => x.IsSaleOpen);
        if (f.ProductGroupId.HasValue) query = query.Where(x => x.ProductGroupId == f.ProductGroupId);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim().ToLower();
            query = query.Where(x => x.Code.ToLower().Contains(s)
                || PgJsonFunctions.JsonText(x.NameI18n, "tr")!.ToLower().Contains(s)
                || (x.SupplierProductCode != null && x.SupplierProductCode.ToLower().Contains(s)));
        }
        return query;
    }

    public static IQueryable<Product> ApplyAll(IQueryable<Product> query, ProductListFilters f, GridRequest? grid)
        => Schema.ApplyFilters(ApplyNamed(query, f), grid);

    /// <summary>Eski `sort` parametresi: newest → CreatedAt desc; diğer/boş → Code asc. Grid sort verilmişse o kazanır.</summary>
    public static IQueryable<Product> ApplySort(IQueryable<Product> query, GridRequest? grid, string? legacySort)
    {
        if (grid?.Sort is { Length: > 0 }) return Schema.ApplySort(query, grid);
        return string.Equals(legacySort, "newest", StringComparison.OrdinalIgnoreCase)
            ? query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            : query.OrderBy(x => x.Code).ThenBy(x => x.Id);
    }

    public static string SourceTypeLabel(string s) => s switch
    {
        "own" => "Kendi", "seller" => "Satıcı", "supply" => "Dış tedarik", _ => s,
    };
}

/// <summary>Ürün listesinin adlandırılmış filtreleri (GET parametreleri / export gövdesi "named").</summary>
public record ProductListFilters(string? Search = null, Guid? ProductGroupId = null, bool ActiveOnly = true);

/// <summary>Excel satırı — export kolon tanımı (Api) bu alanlardan seçer.</summary>
public record ProductExportRow(
    string Code, Dictionary<string, string> NameI18n, string GroupCode, Dictionary<string, string> GroupNameI18n,
    bool IsSaleOpen, decimal BasePrice, decimal? BaseCost, int TaxRate, string SourceType, string? SupplierProductCode,
    int VariantCount, string? Slug, DateTime CreatedAt);

public record ExportProductsQuery(ProductListFilters Filters, GridRequest Grid, int MaxRows, string? LegacySort = null)
    : IRequest<Result<GridExportSource<ProductExportRow>>>;

public class ExportProductsQueryHandler(ICatalogDbContext db) : IRequestHandler<ExportProductsQuery, Result<GridExportSource<ProductExportRow>>>
{
    public async Task<Result<GridExportSource<ProductExportRow>>> Handle(ExportProductsQuery r, CancellationToken ct)
    {
        var q = ProductGrid.ApplyAll(db.Products.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<ProductExportRow>>($"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = ProductGrid.ApplySort(q, r.Grid, r.LegacySort).Select(x => new ProductExportRow(
            x.Code, x.NameI18n, x.ProductGroup.Code, x.ProductGroup.NameI18n,
            x.IsSaleOpen, x.BasePrice, x.BaseCost, x.TaxRate, x.SourceType, x.SupplierProductCode,
            x.Variants.Count, x.Slug, x.CreatedAt));
        return Result.Success(new GridExportSource<ProductExportRow>(count, rows));
    }
}
