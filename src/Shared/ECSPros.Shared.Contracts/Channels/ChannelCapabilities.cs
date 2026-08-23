using System.Text.Json;
using System.Text.Json.Serialization;

namespace ECSPros.Shared.Contracts.Channels;

/// <summary>
/// Satış kanalı yetenek seti (docs/satis-kanali-ortak-kurgu.md §2.1, K1).
/// Kanal TİPİ (PlatformType) varsayılanı taşır; kanal (FirmPlatform) yalnız <see cref="OverridableKeys"/>
/// alanlarını ezebilir. Ekran/iş kuralı kanal tipine değil bu bayraklara bakar.
/// JSON: camelCase, bilinmeyen anahtarlar yok sayılır (ileri uyumluluk).
/// </summary>
public sealed class ChannelCapabilities
{
    /// <summary>Ürün dışarı gönderilir (batch/adapter) — pazaryeri.</summary>
    public bool PushListing { get; set; }
    /// <summary>Dış kategori/özellik/değer eşlemesi gerekir.</summary>
    public bool ExternalTaxonomy { get; set; }
    /// <summary>Hazırlık denetimi seviyesi: light | light_price | full.</summary>
    public string ReadinessLevel { get; set; } = ReadinessLevels.Light;
    /// <summary>Fiyat kaynağı: channel_price_type | channel_price_list | channel_price_readback.</summary>
    public string PriceSource { get; set; } = PriceSources.ChannelPriceType;
    /// <summary>Satış durdurma penceresi kullanılabilir.</summary>
    public bool SaleStopWindow { get; set; } = true;
    /// <summary>Pazaryerinde listeden düşürme (deactivate) batch'i var.</summary>
    public bool RemoteDeactivate { get; set; }
    /// <summary>Üçüncü taraf satıcı (Y3) ürünleri kapsama girebilir.</summary>
    public bool ThirdPartySellerProducts { get; set; }
    /// <summary>Dış tedarik kaynağı (Y4) ürünleri kapsama girebilir.</summary>
    public bool ExternalSupplyProducts { get; set; }
    /// <summary>Sipariş yönü: internal | partner_push | pull.</summary>
    public string OrderDirection { get; set; } = OrderDirections.Internal;
    /// <summary>Kanal stok formülü eşiği (K17): stockQuantity = max(0, netStock − minStock + 1).</summary>
    public int MinStock { get; set; } = 1;
    /// <summary>Kapsama giren ürün otomatik "Kanalda" olur.</summary>
    public bool AutoPublish { get; set; } = true;
    /// <summary>Karşı taraf bizim Partner API'mizi kullanır (dropship bayi çeker).</summary>
    public bool PullsFromPartnerApi { get; set; }

    public static class ReadinessLevels { public const string Light = "light", LightPrice = "light_price", Full = "full"; }
    public static class PriceSources { public const string ChannelPriceType = "channel_price_type", ChannelPriceList = "channel_price_list", ChannelPriceReadback = "channel_price_readback"; }
    public static class OrderDirections { public const string Internal = "internal", PartnerPush = "partner_push", Pull = "pull"; }

    /// <summary>Kanal bazında ezilebilen alanlar (camelCase JSON anahtarları). Diğerleri yalnız tipte.</summary>
    public static readonly IReadOnlyList<string> OverridableKeys = new[]
    {
        "thirdPartySellerProducts", "externalSupplyProducts", "autoPublish", "minStock",
    };

    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>Bilinen platform tipi kodları için varsayılan yetenek seti (§2.1 tablosu).</summary>
    public static ChannelCapabilities DefaultsFor(string? platformTypeCode, bool isMarketplaceFallback = false)
    {
        switch ((platformTypeCode ?? string.Empty).ToLowerInvariant())
        {
            case "site":
                return new ChannelCapabilities { ThirdPartySellerProducts = true, ExternalSupplyProducts = true };
            case "mobile_app":
            case "pos":
                return new ChannelCapabilities { ThirdPartySellerProducts = true, ExternalSupplyProducts = true };
            case "dropship_partner":
                return new ChannelCapabilities
                {
                    ReadinessLevel = ReadinessLevels.LightPrice,
                    PriceSource = PriceSources.ChannelPriceList,
                    OrderDirection = OrderDirections.PartnerPush,
                    MinStock = 3,
                    PullsFromPartnerApi = true,
                };
            case "trendyol":
            case "hepsiburada":
            case "n11":
            case "amazon":
            case "ciceksepeti":
            case "pazarama":
                return Marketplace();
            default:
                return isMarketplaceFallback ? Marketplace() : new ChannelCapabilities();
        }
    }

    public static ChannelCapabilities Marketplace() => new()
    {
        PushListing = true,
        ExternalTaxonomy = true,
        ReadinessLevel = ReadinessLevels.Full,
        PriceSource = PriceSources.ChannelPriceReadback,
        RemoteDeactivate = true,
        OrderDirection = OrderDirections.Pull,
    };

    public static ChannelCapabilities? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<ChannelCapabilities>(json, Json); }
        catch (JsonException) { return null; }
    }

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public ChannelCapabilities Clone() => JsonSerializer.Deserialize<ChannelCapabilities>(ToJson(), Json)!;

    /// <summary>
    /// Kanal ezmelerini uygular. Yalnız <see cref="OverridableKeys"/> anahtarları dikkate alınır;
    /// diğerleri sessizce yok sayılır (tip sahibi olmayan alan ezilemez).
    /// </summary>
    public ChannelCapabilities WithOverrides(string? overridesJson)
    {
        if (string.IsNullOrWhiteSpace(overridesJson)) return Clone();
        JsonDocument doc;
        try { doc = JsonDocument.Parse(overridesJson); }
        catch (JsonException) { return Clone(); }
        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return Clone();
            var result = Clone();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!OverridableKeys.Contains(prop.Name, StringComparer.OrdinalIgnoreCase)) continue;
                if (prop.Value.ValueKind == JsonValueKind.Null) continue;
                switch (prop.Name.ToLowerInvariant())
                {
                    case "thirdpartysellerproducts": if (prop.Value.ValueKind is JsonValueKind.True or JsonValueKind.False) result.ThirdPartySellerProducts = prop.Value.GetBoolean(); break;
                    case "externalsupplyproducts":   if (prop.Value.ValueKind is JsonValueKind.True or JsonValueKind.False) result.ExternalSupplyProducts = prop.Value.GetBoolean(); break;
                    case "autopublish":              if (prop.Value.ValueKind is JsonValueKind.True or JsonValueKind.False) result.AutoPublish = prop.Value.GetBoolean(); break;
                    case "minstock":                 if (prop.Value.TryGetInt32(out var ms) && ms >= 0) result.MinStock = ms; break;
                }
            }
            return result;
        }
    }

    /// <summary>Ezme JSON'unu temizler: yalnız izinli anahtarlar kalır; boşsa null.</summary>
    public static string? SanitizeOverrides(string? overridesJson)
    {
        if (string.IsNullOrWhiteSpace(overridesJson)) return null;
        JsonDocument doc;
        try { doc = JsonDocument.Parse(overridesJson); }
        catch (JsonException) { return null; }
        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            var dict = new Dictionary<string, object?>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var key = OverridableKeys.FirstOrDefault(k => string.Equals(k, prop.Name, StringComparison.OrdinalIgnoreCase));
                if (key is null || prop.Value.ValueKind == JsonValueKind.Null) continue;
                dict[key] = prop.Value.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Number when prop.Value.TryGetInt32(out var n) && n >= 0 => n,
                    _ => null,
                };
                if (dict[key] is null) dict.Remove(key);
            }
            return dict.Count == 0 ? null : JsonSerializer.Serialize(dict, Json);
        }
    }
}

/// <summary>Kanalın ETKİN yetenek setini (tip varsayılanı + kanal ezmesi) çözer; kısa süreli önbellekli.</summary>
public interface IChannelCapabilityResolver
{
    Task<ChannelCapabilities> GetAsync(Guid firmPlatformId, CancellationToken ct = default);
    /// <summary>Platform tipi kodu için varsayılan (kanal ezmesi yok).</summary>
    ChannelCapabilities DefaultsFor(string platformTypeCode);
    void Invalidate(Guid? firmPlatformId = null);
}
