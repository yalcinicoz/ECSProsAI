using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// G1: yayınlanmış versiyonlu vitrin snapshot'ı (spec: PublishedHomepageSnapshot).
/// "Yayınla" taslak page_blocks ağacını doğrulayıp TÜM yerleşimleri kapsayan tek bir
/// JSON'a dondurur; canlı site YALNIZ platformun aktif snapshot'ını okur, taslak
/// tablolara join atmaz. Version platform içinde artan sayıdır ve cache anahtarına
/// girer (yeni yayın eski cache'i otomatik geçersizleştirir). Rollback eski versiyonun
/// IsActive'ini geri açmaktır — JsonData asla değiştirilmez.
/// </summary>
public class PublishedSnapshot : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public int Version { get; set; }
    public string JsonData { get; set; } = "{}";
    public DateTime PublishedAt { get; set; }
    public Guid? PublishedBy { get; set; }
    /// <summary>Platform başına en fazla bir aktif snapshot; canlı site bunu okur.</summary>
    public bool IsActive { get; set; }
    public string Status { get; set; } = "published"; // published | superseded | rolledback
    public string? Note { get; set; }
}
