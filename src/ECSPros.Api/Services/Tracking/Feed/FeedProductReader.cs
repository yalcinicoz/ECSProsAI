using System.Text.Json;
using ECSPros.Integration.Application.Services;
using ECSPros.Storefront.Application.Queries.GetProductsLeafChannelCategories;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.Tracking.Feed;

/// <summary>Feed satırı (varyant = item).</summary>
public sealed record FeedItem(
    Guid ProductId, string ProductCode, string Title, string? Description,
    Guid VariantId, string Sku, string? Barcode,
    decimal Price, decimal? CompareAtPrice, int Stock,
    string? Slug, string? Color, Guid? ColorValueId, string? Size, string? Brand, string? Gender,
    string? CategoryPath, string? GoogleCategory, IReadOnlyList<string> ImageFiles);

/// <summary>
/// İE-5 Faz E (2026-08-22): Merchant Center / Meta katalog feed'i için kanalın satışa açık
/// ürün+varyantlarını RAW SQL ile okur (28K ürün / 300K varyant — EF nesne grafiği yerine düz satırlar).
/// Kaynaklar: catalog.products/variants (IsSaleOpen, IsActive) ∩ storefront.channel_products (kanalda aktif),
/// storefront.channel_variants (kanal fiyatı/compare/slug), definition attribute'ları (renk/beden varyant;
/// marka/cinsiyet ürün), inventory sellable stok (IsSellableOnline kısım + aktif depo), catalog.product_images
/// (Active; varyant görseli > aynı renk kardeş > ürün kapak), yaprak kanal kategorisi (breadcrumb kural motoru)
/// + GoogleCategoryId.
/// </summary>
public sealed class FeedProductReader(
    IIntegrationDbContext db,
    IStorefrontDbContext sfDb,
    IMediator mediator)
{
    private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web);

    private sealed record VarRow(Guid ProductId, string ProductCode, string ProductName, string? Description,
        decimal ProductBasePrice, Guid VariantId, string Sku, string? Barcode, decimal VariantBasePrice,
        decimal? ChannelPrice, decimal? CompareAtPrice, string? Slug);
    private sealed record AttrRow(Guid OwnerId, string Code, string NameI18n, Guid ValueId);
    private sealed record StockRow(Guid VariantId, long Qty);
    private sealed record ImgRow(Guid ProductId, Guid? VariantId, string FileName, int SortOrder, bool IsProductCover);
    private sealed record CatRow(Guid Id, Guid? ParentId, string NameI18n, string? GoogleCategoryId);

    public async Task<List<FeedItem>> ReadAsync(Guid platformId, CancellationToken ct)
    {
        var dbf = db.Database;

        var varyantlar = await dbf.SqlQuery<VarRow>($"""
            SELECT p."Id" AS "ProductId", p."Code" AS "ProductCode", p."NameI18n"::text AS "ProductName",
                   p."DescriptionI18n"::text AS "Description", p."BasePrice" AS "ProductBasePrice",
                   v."Id" AS "VariantId", v."Sku" AS "Sku", v."Barcode" AS "Barcode", v."BasePrice" AS "VariantBasePrice",
                   cv."Price" AS "ChannelPrice", cv."CompareAtPrice" AS "CompareAtPrice", cv."Slug" AS "Slug"
            FROM catalog.products p
            JOIN catalog.product_variants v ON v."ProductId" = p."Id" AND v."IsActive" AND NOT v."IsDeleted"
            LEFT JOIN storefront.channel_variants cv ON cv."VariantId" = v."Id" AND cv."FirmPlatformId" = {platformId}
                 AND cv."IsActive" AND NOT cv."IsDeleted"
            WHERE p."IsSaleOpen" AND NOT p."IsDeleted"
              AND EXISTS (SELECT 1 FROM storefront.channel_products cp
                          WHERE cp."ProductId" = p."Id" AND cp."FirmPlatformId" = {platformId}
                            AND cp."IsActive" AND NOT cp."IsDeleted"
                            AND (cp."SaleStoppedFrom" IS NULL OR cp."SaleStoppedFrom" > now()
                                 OR (cp."SaleStoppedUntil" IS NOT NULL AND cp."SaleStoppedUntil" < now())))
            """).ToListAsync(ct);
        if (varyantlar.Count == 0) return new();

        var varAttr = await dbf.SqlQuery<AttrRow>($"""
            SELECT va."VariantId" AS "OwnerId", t."Code" AS "Code", av."NameI18n"::text AS "NameI18n", av."Id" AS "ValueId"
            FROM catalog.product_variant_attributes va
            JOIN definition.attribute_types t ON t."Id" = va."AttributeTypeId" AND t."Code" IN ('renk','beden')
            JOIN definition.attribute_values av ON av."Id" = va."AttributeValueId"
            JOIN catalog.product_variants v ON v."Id" = va."VariantId" AND v."IsActive" AND NOT v."IsDeleted"
            JOIN catalog.products p ON p."Id" = v."ProductId" AND p."IsSaleOpen" AND NOT p."IsDeleted"
            WHERE NOT va."IsDeleted"
            """).ToListAsync(ct);
        var urunAttr = await dbf.SqlQuery<AttrRow>($"""
            SELECT pa."ProductId" AS "OwnerId", t."Code" AS "Code", av."NameI18n"::text AS "NameI18n", av."Id" AS "ValueId"
            FROM catalog.product_attributes pa
            JOIN definition.attribute_types t ON t."Id" = pa."AttributeTypeId" AND t."Code" IN ('marka','cinsiyet')
            JOIN definition.attribute_values av ON av."Id" = pa."AttributeValueId"
            JOIN catalog.products p ON p."Id" = pa."ProductId" AND p."IsSaleOpen" AND NOT p."IsDeleted"
            WHERE NOT pa."IsDeleted" AND pa."AttributeValueId" IS NOT NULL
            """).ToListAsync(ct);
        var stoklar = await dbf.SqlQuery<StockRow>($"""
            SELECT s."VariantId" AS "VariantId", SUM(s."Quantity" - s."ReservedQuantity")::bigint AS "Qty"
            FROM inventory.inv_stocks s
            JOIN inventory.inv_warehouse_sections sec ON sec."Id" = s."SectionId"
            JOIN inventory.inv_warehouses w ON w."Id" = s."WarehouseId"
            WHERE s."BinId" IS NOT NULL AND sec."IsSellableOnline" AND w."IsActive"
            GROUP BY s."VariantId"
            """).ToListAsync(ct);
        var gorseller = await dbf.SqlQuery<ImgRow>($"""
            SELECT i."ProductId" AS "ProductId", i."VariantId" AS "VariantId", i."FileName" AS "FileName",
                   i."SortOrder" AS "SortOrder", i."IsProductCover" AS "IsProductCover"
            FROM catalog.product_images i
            JOIN catalog.products p ON p."Id" = i."ProductId" AND p."IsSaleOpen" AND NOT p."IsDeleted"
            WHERE i."Status" = 'Active' AND NOT i."IsDeleted"
            """).ToListAsync(ct);

        // Yaprak kanal kategorisi (breadcrumb kuralı) + kategori ağacı (product_type yolu, Google kategori)
        var yaprak = await mediator.Send(new GetProductsLeafChannelCategoriesQuery(platformId, null), ct);
        var yaprakByUrun = yaprak.IsSuccess ? yaprak.Value!.GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.First().CategoryId) : new();
        var kategoriler = await sfDb.ChannelCategories.AsNoTracking()
            .Where(c => c.FirmPlatformId == platformId)
            .Select(c => new CatRow(c.Id, c.ParentId, "", c.GoogleCategoryId))
            .ToListAsync(ct);
        var katAd = await sfDb.ChannelCategories.AsNoTracking()
            .Where(c => c.FirmPlatformId == platformId)
            .Select(c => new { c.Id, c.NameI18n })
            .ToDictionaryAsync(c => c.Id, c => TrAd(c.NameI18n), ct);
        var katById = kategoriler.ToDictionary(c => c.Id);
        string? Yol(Guid id)
        {
            var parcalar = new List<string>(); var guard = 0;
            for (Guid? cur = id; cur is { } c && katById.TryGetValue(c, out var k) && guard++ < 10; cur = k.ParentId)
                parcalar.Insert(0, katAd.TryGetValue(c, out var ad) ? ad : "");
            return parcalar.Count == 0 ? null : string.Join(" > ", parcalar.Where(x => x.Length > 0));
        }
        string? Google(Guid id)
        {
            var guard = 0;
            for (Guid? cur = id; cur is { } c && katById.TryGetValue(c, out var k) && guard++ < 10; cur = k.ParentId)
                if (!string.IsNullOrWhiteSpace(k.GoogleCategoryId)) return k.GoogleCategoryId;
            return null;
        }

        var renkByVar = new Dictionary<Guid, (string Ad, Guid Id)>(); var bedenByVar = new Dictionary<Guid, string>();
        foreach (var a in varAttr)
        {
            if (a.Code == "renk") renkByVar[a.OwnerId] = (TrAdJson(a.NameI18n), a.ValueId);
            else bedenByVar[a.OwnerId] = TrAdJson(a.NameI18n);
        }
        var markaByUrun = new Dictionary<Guid, string>(); var cinsByUrun = new Dictionary<Guid, string>();
        foreach (var a in urunAttr)
        {
            if (a.Code == "marka") markaByUrun[a.OwnerId] = TrAdJson(a.NameI18n);
            else cinsByUrun[a.OwnerId] = TrAdJson(a.NameI18n);
        }
        var stokByVar = stoklar.ToDictionary(s => s.VariantId, s => (int)Math.Max(0, s.Qty));
        var imgByUrun = gorseller.GroupBy(g => g.ProductId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.IsProductCover).ThenBy(x => x.SortOrder).ToList());
        var varyantByUrun = varyantlar.GroupBy(v => v.ProductId).ToDictionary(g => g.Key, g => g.Select(x => x.VariantId).ToList());
        // kanal slug'ı renk (kart) düzeyindedir — varyantın kendi slug'ı yoksa aynı renkteki kardeşinki kullanılır
        var slugByVar = varyantlar.Where(v => !string.IsNullOrWhiteSpace(v.Slug)).ToDictionary(v => v.VariantId, v => v.Slug!);

        var sonuc = new List<FeedItem>(varyantlar.Count);
        foreach (var v in varyantlar)
        {
            var fiyat = v.ChannelPrice is > 0 ? v.ChannelPrice.Value : (v.VariantBasePrice > 0 ? v.VariantBasePrice : v.ProductBasePrice);
            if (fiyat <= 0) continue;
            renkByVar.TryGetValue(v.VariantId, out var renk);
            bedenByVar.TryGetValue(v.VariantId, out var beden);
            // görsel: varyantın kendi > aynı renkteki kardeş varyantın > ürün kapak/diğerleri
            var urunGorselleri = imgByUrun.TryGetValue(v.ProductId, out var gl) ? gl : new List<ImgRow>();
            var kardesler = renk.Id != Guid.Empty && varyantByUrun.TryGetValue(v.ProductId, out var kl)
                ? kl.Where(k => renkByVar.TryGetValue(k, out var r2) && r2.Id == renk.Id).ToHashSet()
                : new HashSet<Guid> { v.VariantId };
            var dosyalar = urunGorselleri.Where(g => g.VariantId == v.VariantId).Select(g => g.FileName)
                .Concat(urunGorselleri.Where(g => g.VariantId is { } vid && vid != v.VariantId && kardesler.Contains(vid)).Select(g => g.FileName))
                .Concat(urunGorselleri.Where(g => g.VariantId is null).Select(g => g.FileName))
                .Distinct().Take(11).ToList();
            var katId = yaprakByUrun.TryGetValue(v.ProductId, out var k) ? k : (Guid?)null;
            var slug = v.Slug;
            if (string.IsNullOrWhiteSpace(slug))
                slug = kardesler.Select(kid => slugByVar.TryGetValue(kid, out var ks) ? ks : null).FirstOrDefault(x => x is not null);
            sonuc.Add(new FeedItem(
                v.ProductId, v.ProductCode, TrAdJson(v.ProductName), TrAdJsonOrNull(v.Description),
                v.VariantId, string.IsNullOrWhiteSpace(v.Sku) ? v.ProductCode : v.Sku, v.Barcode,
                fiyat, v.CompareAtPrice is > 0 && v.CompareAtPrice > fiyat ? v.CompareAtPrice : null,
                stokByVar.TryGetValue(v.VariantId, out var st) ? st : 0,
                slug, renk.Ad, renk.Id == Guid.Empty ? null : renk.Id, beden,
                markaByUrun.TryGetValue(v.ProductId, out var m) ? m : null,
                cinsByUrun.TryGetValue(v.ProductId, out var c) ? c : null,
                katId is { } kid ? Yol(kid) : null, katId is { } kid2 ? Google(kid2) : null, dosyalar));
        }
        return sonuc;
    }

    private static string TrAd(Dictionary<string, string>? d) => d is null ? "" : (d.TryGetValue("tr", out var t) ? t : d.Values.FirstOrDefault() ?? "");
    private static string TrAdJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "";
        try { return TrAd(JsonSerializer.Deserialize<Dictionary<string, string>>(json, J)); } catch { return json; }
    }
    private static string? TrAdJsonOrNull(string? json) { var v = TrAdJson(json); return string.IsNullOrWhiteSpace(v) ? null : v; }
}
