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
    public static readonly string[] ImageStates = { "none", "partial", "full" };
    public static readonly string[] VideoStates = { "var", "yok" };   // panel seçicisi Var/Yok (2026-09-11)

    public static readonly GridSchema<Product> Schema = new GridSchema<Product>()
        .Text("code", p => p.Code)
        .Text("supplierProductCode", p => p.SupplierProductCode)
        .Text("slug", p => p.Slug)
        .Text("name", p => GridJson.Text(p.NameI18n, "tr"))
        .Number("variantCount", p => p.Variants.Count)
        .Enum("sourceType", p => p.SourceType, SourceTypes)
        .Bool("isSaleOpen", p => p.IsSaleOpen)
        .Number("basePrice", p => p.BasePrice)
        .Number("taxRate", p => p.TaxRate)
        .Date("createdAt", p => p.CreatedAt)
        .Guid("productGroupId", p => p.ProductGroupId)
        .Guid("supplierId", p => p.SupplierId)
        // 2026-09-11 kapsamlı filtre: görsel/stok istatistikleri mv_product_stats'tan (5 dk tazelik; kayıt yoksa 0/none)
        .Enum("imageState", p => p.Stats != null ? p.Stats.ImageState : "none", ImageStates)
        .Number("imageCount", p => p.Stats != null ? p.Stats.ImageCount : 0)
        .Date("lastImageAt", p => p.Stats != null ? p.Stats.LastImageAt : null)
        .Enum("videoState", p => p.Stats != null && p.Stats.VideoCount > 0 ? "var" : "yok", VideoStates)
        .Number("stock", p => p.Stats != null ? p.Stats.StockQuantity : 0)
        .Number("stockAvailable", p => p.Stats != null ? p.Stats.StockAvailable : 0)
        .Sort("imageState", p => p.Stats != null ? p.Stats.ImageState : "none")
        .Sort("imageCount", p => p.Stats != null ? p.Stats.ImageCount : 0)
        .Sort("lastImageAt", p => p.Stats != null ? p.Stats.LastImageAt : null)
        .Sort("videoState", p => p.Stats != null && p.Stats.VideoCount > 0 ? "var" : "yok")
        .Sort("stock", p => p.Stats != null ? p.Stats.StockQuantity : 0)
        .Sort("stockAvailable", p => p.Stats != null ? p.Stats.StockAvailable : 0)
        .Sort("group", p => GridJson.Text(p.ProductGroup.NameI18n, "tr"))
        .Sort("code", p => p.Code)
        .Sort("name", p => GridJson.Text(p.NameI18n, "tr"))
        .Sort("variantCount", p => p.Variants.Count)
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

    /// <summary>Şema filtreleri + özel işlenen `barcode` (varyant barkodu: eq tam / contains içerir — koleksiyon alanı şemaya girmez).</summary>
    public static IQueryable<Product> ApplyAll(IQueryable<Product> query, ProductListFilters f, GridRequest? grid)
    {
        query = ApplyNamed(query, f);
        var barcode = grid?.Filters.FirstOrDefault(x => string.Equals(x.Field, "barcode", StringComparison.OrdinalIgnoreCase));
        if (barcode is not null && !string.IsNullOrWhiteSpace(barcode.Value))
        {
            var b = barcode.Value.Trim();
            query = string.Equals(barcode.Op, "contains", StringComparison.OrdinalIgnoreCase)
                ? query.Where(p => p.Variants.Any(v => v.Barcode != null && v.Barcode.Contains(b)))
                : query.Where(p => p.Variants.Any(v => v.Barcode == b));
        }
        // 2026-09-11 ürün ÖZELLİK filtresi (eski /urun/urun-yonetim "Seçilen Özellikler"): değer biçimi
        // "tipId:degerId,degerId;tipId2:degerId" — aynı özellik içinde VEYA, özellikler arasında VE (eski kuralla aynı).
        // Eşleşme: ürün düzeyi özellik (product_attributes) YA DA herhangi bir varyantın özelliği (product_variant_attributes).
        var attrs = grid?.Filters.FirstOrDefault(x => string.Equals(x.Field, "attrs", StringComparison.OrdinalIgnoreCase));
        if (attrs is not null && !string.IsNullOrWhiteSpace(attrs.Value))
        {
            foreach (var grupMetni in attrs.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parcalar = grupMetni.Split(':', 2);
                if (parcalar.Length != 2 || !System.Guid.TryParse(parcalar[0], out var tipId)) throw new GridException("'attrs' filtresi 'tipId:degerId,degerId;…' biçiminde olmalıdır.");
                var degerler = parcalar[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(d => System.Guid.TryParse(d, out var g) ? g : (Guid?)null).Where(g => g.HasValue).Select(g => g!.Value).ToList();
                if (degerler.Count == 0) continue;
                query = query.Where(p =>
                    p.Attributes.Any(a => a.AttributeTypeId == tipId && a.AttributeValueId != null && degerler.Contains(a.AttributeValueId.Value))
                    || p.Variants.Any(v => v.VariantAttributes.Any(va => va.AttributeTypeId == tipId && degerler.Contains(va.AttributeValueId))));
            }
        }
        return Schema.ApplyFilters(query, grid, "barcode", "attrs");
    }

    /// <summary>
    /// Sıralama: grid `sort` şema beyaz listesindeyse şema; değilse (örn. eski istemcilerin `sort=newest`'i — aynı query parametresi
    /// GridRequest.Sort'a da düşer) legacy kural: newest → CreatedAt desc; diğer/boş → Code asc. Bilinmeyen değer 400 ÜRETMEZ (2026-09-08 düzeltmesi).
    /// </summary>
    public static IQueryable<Product> ApplySort(IQueryable<Product> query, GridRequest? grid, string? legacySort)
    {
        if (grid?.Sort is { Length: > 0 } key && Schema.SortableFields.Contains(key, StringComparer.OrdinalIgnoreCase)) return Schema.ApplySort(query, grid);
        legacySort ??= grid?.Sort;
        return string.Equals(legacySort, "newest", StringComparison.OrdinalIgnoreCase)
            ? query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            : query.OrderBy(x => x.Code).ThenBy(x => x.Id);
    }

    public static string ImageStateLabel(string s) => s switch { "full" => "Var", "partial" => "Kısmi", "none" => "Yok", _ => s };
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
    int VariantCount, string? Slug, DateTime CreatedAt,
    string ImageState = "none", int ImageCount = 0, int StockQuantity = 0, int StockAvailable = 0, bool HasVideo = false);

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
            x.Variants.Count, x.Slug, x.CreatedAt,
            x.Stats != null ? x.Stats.ImageState : "none", x.Stats != null ? x.Stats.ImageCount : 0,
            x.Stats != null ? x.Stats.StockQuantity : 0, x.Stats != null ? x.Stats.StockAvailable : 0, x.Stats != null && x.Stats.VideoCount > 0));
        return Result.Success(new GridExportSource<ProductExportRow>(count, rows));
    }
}
