using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// E6: üye koleksiyonu (alışveriş listesi/kombin). Moderasyonlu: pending doğar, admin
/// onaylarsa approved — Faz G "Koleksiyonlar bloğu" YALNIZ approved+IsPublic olanları
/// gösterebilir (spec şartı). ShareCode paylaşılabilir link için (public koleksiyon
/// sayfası Faz G'de). IsQuickSave: kart/detay bookmark'ının otomatik "Kaydedilenler"
/// koleksiyonu (üye başına bir tane; gizli ve paylaşımsız doğar).
/// </summary>
public class Collection : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public Guid MemberId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public bool IsShareable { get; set; }
    public string ShareCode { get; set; } = string.Empty;
    public string Status { get; set; } = "pending"; // pending | approved | rejected
    public int ViewCount { get; set; }
    public bool IsQuickSave { get; set; }
    public DateTime? ModeratedAt { get; set; }

    public ICollection<CollectionItem> Items { get; set; } = new List<CollectionItem>();
}
