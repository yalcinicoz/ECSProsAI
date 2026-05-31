using System.Text.Json;

namespace ECSPros.Catalog.Application.Helpers;

public class CategoryFilterRules
{
    // Ürün grubu
    public List<Guid>? ProductGroupIds { get; set; }

    // Temel fiyat aralığı
    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }

    // Platform fiyatı (FirmPlatformVariant.Price — sorgu zamanında platform biliniyorsa uygulanır)
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

    // Ürün durumu: true=sadece aktif, false=sadece pasif, null=tümü
    public bool? IsActive { get; set; }

    // Stok miktarı aralığı (toplam mevcut stok, tüm depolar)
    public int? StockMin { get; set; }
    public int? StockMax { get; set; }

    // Oluşturma tarihi (mutlak)
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    /// <summary>Son X gün içinde oluşturulmuş. Mutlak tarihten önce uygulanır.</summary>
    public int? CreatedAfterDays { get; set; }

    // Resim güncelleme tarihi
    public DateTime? ImageUpdatedAfter { get; set; }
    public DateTime? ImageUpdatedBefore { get; set; }
    /// <summary>Son X gün içinde resmi güncellenmiş.</summary>
    public int? ImageUpdatedAfterDays { get; set; }

    // Etiket filtresi (Product.Tags — en az biri eşleşmeli)
    public List<string>? Tags { get; set; }

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
