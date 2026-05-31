namespace ECSPros.Catalog.Domain.Entities;

/// <summary>Katalog modülü uygulama ayarları (key-value).</summary>
public class CatalogSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
