using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

/// <summary>
/// Yeniden kullanılabilir adlandırılmış filtre tanımı.
/// Kategoriler ve diğer yerlerden referans alınabilir.
/// </summary>
public class FilterPreset : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    /// <summary>Filtrenin ne yaptığını anlatan insan dili açıklama.</summary>
    public string? Description { get; set; }
    /// <summary>CategoryFilterRules yapısıyla uyumlu JSONB filtre tanımı.</summary>
    public Dictionary<string, object> FilterDef { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
