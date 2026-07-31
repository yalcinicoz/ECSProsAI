using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Promotion.Domain.Entities;

/// <summary>
/// Kampanya TİPİ = platformdan bağımsız yetenek (definition katmanı, <c>definition.campaign_types</c>).
/// Yalnız geliştirici/platform yönetimi (definition.manage) tanımlar; platformlar bu tipi seçip
/// <c>SettingsSchema</c> şablonunu <c>Campaign.Settings</c> ile doldurarak uygular.
/// (Bkz. docs/kampanya-uctan-uca-plani.md §2.5)
/// </summary>
public class CampaignType : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public Dictionary<string, string>? DescriptionI18n { get; set; }
    public string HandlerClass { get; set; } = string.Empty;

    /// <summary>Parametre giriş ŞABLONU (jsonb liste) — admin kampanya formu bundan üretilir.</summary>
    public List<CampaignSchemaField>? SettingsSchema { get; set; }

    /// <summary>cart | product | shipping | member — kampanyanın etki alanı (motor/gösterim ayrımı).</summary>
    public string Scope { get; set; } = "product";

    /// <summary>Ürün seçimi (tümü/filtre/manuel) destekler mi (product-scoped tipler).</summary>
    public bool RequiresProducts { get; set; }

    /// <summary>Kartta/detayda ürün-bazlı "kampanyalı birim fiyat" gösterilebilir mi
    /// (yalnız ürün-bazlı hesaplanabilen tipler; sepet-bağımlılar yalnız "Sepette").</summary>
    public bool ProductPriceDisplay { get; set; }

    public bool IsStackable { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public ICollection<Campaign> Campaigns { get; set; } = new List<Campaign>();
}
