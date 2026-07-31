using System.Text.Json.Serialization;

namespace ECSPros.Promotion.Domain.Entities;

/// <summary>
/// Kampanya tipi parametre şablonu alanı — <c>definition.campaign_types.SettingsSchema</c> (jsonb liste).
/// Core'daki <c>PlatformSchemaField</c> deseninin kampanya karşılığı; ek olarak seçim kutusu,
/// birim, aralık ve koşullu görünürlük taşır. Admin kampanya formu bu listeden üretilir;
/// platform değerleri <c>Campaign.Settings</c>'e doldurur. (Bkz. docs/kampanya-tip-sablonlari-taslak.md)
/// </summary>
public class CampaignSchemaField
{
    public string Key { get; set; } = string.Empty;
    public Dictionary<string, string> LabelI18n { get; set; } = new();

    /// <summary>percent | money | integer | number | boolean | select</summary>
    public string Type { get; set; } = "number";

    public bool Required { get; set; }

    /// <summary>Gösterim birimi (% | ₺ | adet) — opsiyonel.</summary>
    public string? Unit { get; set; }

    public decimal? Min { get; set; }
    public decimal? Max { get; set; }

    /// <summary>Varsayılan değer (opsiyonel) — jsonb, tipe göre string/number/bool.</summary>
    public object? Default { get; set; }

    /// <summary>type=select için seçenekler.</summary>
    public List<CampaignSchemaFieldOption>? Options { get; set; }

    /// <summary>Alanın hangi koşulda görüneceği (opsiyonel).</summary>
    public CampaignSchemaFieldCondition? VisibleWhen { get; set; }

    public Dictionary<string, string>? HelpI18n { get; set; }
}

public class CampaignSchemaFieldOption
{
    public string Value { get; set; } = string.Empty;
    public Dictionary<string, string> LabelI18n { get; set; } = new();
}

/// <summary>Koşullu görünürlük: başka bir alanın değerine göre. equals veya notEquals'tan biri.</summary>
public class CampaignSchemaFieldCondition
{
    public string Field { get; set; } = string.Empty;

    [JsonPropertyName("equals")]
    public string? EqualsValue { get; set; }

    [JsonPropertyName("notEquals")]
    public string? NotEqualsValue { get; set; }
}
