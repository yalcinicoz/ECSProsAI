using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// Ürün Kartı F2 (2026-08-09): kart duyuruları — kartın değişken alanlarında (1/2/3)
/// rotasyonla dönen elle tanımlı mesajlar. Panel Storefront → Ürün Kartı → Kart Mesajları.
/// Kapsam: all (kanalın tüm ürünleri) | category (seçili kanal kategorileri) |
/// products (ürün kodu listesi). Tarih penceresi boş = süresiz.
/// </summary>
public class CardMessage : BaseEntity
{
    public Guid FirmPlatformId { get; set; }

    /// <summary>1 = görsel altı bant, 2 = ürün adı altı satır, 3 = puan altı satır.</summary>
    public int Slot { get; set; } = 1;

    public Dictionary<string, string> MessageI18n { get; set; } = new();

    /// <summary>Font Awesome sınıf adı (örn. fa-truck) — alan 2/3'te mesajın önünde.</summary>
    public string? Icon { get; set; }

    /// <summary>Palet anahtarı: yesil | turuncu | bordo | pembe (boş = varsayılan renk).
    /// Alan 2/3'te metin rengi (ms-metin-*), alan 1'de bant arka planı.</summary>
    public string? Color { get; set; }

    /// <summary>all | category | products</summary>
    public string ScopeType { get; set; } = "all";

    /// <summary>ScopeType=category: kanal kategorisi Id listesi (jsonb).</summary>
    public List<Guid>? ScopeCategoryIds { get; set; }

    /// <summary>ScopeType=products: ürün kodu listesi (jsonb).</summary>
    public List<string>? ScopeProductCodes { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public bool IsLiveAt(DateTime now) =>
        IsActive
        && (!StartDate.HasValue || StartDate.Value <= now)
        && (!EndDate.HasValue || EndDate.Value >= now);
}
