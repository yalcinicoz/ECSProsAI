using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Integration.Domain.Entities;

/// <summary>
/// Pazaryeri hata sınıflandırma kalıbı (§4.3): ham hata metni → normalize ErrorCode.
/// DB'de tutulur ki yeni hata kalıbı kod değişikliği gerektirmeden eklenebilsin;
/// koddaki yerleşik varsayılanların ÜZERİNE uygulanır (DB kalıbı önce denenir).
/// </summary>
public class MarketplaceErrorPattern : BaseEntity
{
    public string Marketplace { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;        // .NET regex (case-insensitive uygulanır)
    /// <summary>category_conflict | missing_attribute | invalid_value | duplicate_barcode |
    /// rate_limited | unknown …</summary>
    public string ErrorCode { get; set; } = "unknown";
    /// <summary>category_conflict'te beklenen kategori adını/kimliğini yakalayan regex grubu (0 = yok).</summary>
    public int SuggestedCategoryGroup { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
