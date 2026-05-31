using System.Text.Json;

namespace ECSPros.Catalog.Application.Helpers;

public class CategoryFilterRules
{
    // Ürün grubu
    public List<Guid>? ProductGroupIds { get; set; }

    // Fiyat — ürünün BasePrice'ına göre
    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }

    // Platform fiyatı — FirmPlatformVariant.Price'a göre (sorgu zamanında platform biliniyorsa uygulanır)
    public decimal? PlatformPriceMin { get; set; }
    public decimal? PlatformPriceMax { get; set; }

    // İndirim
    public int? DiscountMinPercent { get; set; }

    // KDV oranı
    public int? TaxRateMin { get; set; }
    public int? TaxRateMax { get; set; }

    // Özellik filtresi
    public List<AttributeFilterRule>? AttributeFilters { get; set; }

    // Tedarikçi (Product.SupplierId → CurrentAccount.Id)
    public List<Guid>? SupplierIds { get; set; }

    // Kategori (ürün şu kategorilerde olmalı)
    public List<Guid>? CategoryIds { get; set; }

    // Ürün durumu: true=sadece aktif, false=sadece pasif, null=tümü
    public bool? IsActive { get; set; }

    // Stok: true=stokta var, false=stok yok, null=filtre yok
    public bool? HasStock { get; set; }

    // Oluşturma tarihi (yeni ürün filtresi)
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }

    private static readonly JsonSerializerOptions _opts = new() { PropertyNameCaseInsensitive = true };

    public static CategoryFilterRules? From(Dictionary<string, object>? raw)
    {
        if (raw is null) return null;
        var json = JsonSerializer.Serialize(raw);
        return JsonSerializer.Deserialize<CategoryFilterRules>(json, _opts);
    }
}

public class AttributeFilterRule
{
    public Guid AttributeTypeId { get; set; }
    public List<Guid> ValueIds { get; set; } = new();
}
