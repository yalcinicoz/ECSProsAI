using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Promotion.Domain.Entities;

/// <summary>
/// P3a (2026-08-11, K1 alt-kararı): pazaryeri kampanyasına SATICI katılımı — satıcı katılıp
/// katılmayacağını ve hangi ürünlerle katılacağını belirtir (opt-in). ProductIds boş liste =
/// satıcının kampanya kapsamına giren TÜM ürünleriyle katılım. Katılım yoksa (ve kampanya
/// RequiresSupplierOptIn ise) satıcının ürünleri kampanyada değerlendirilmez; hakedişte
/// kampanya oranı/indirim paylaşımı da yalnız katılımlı satırlara uygulanır.
/// </summary>
public class CampaignSupplierParticipation : BaseEntity
{
    public Guid CampaignId { get; set; }
    /// <summary>Satıcının cari hesabı (CurrentAccount) — partner API owner'ı ile aynı kimlik.</summary>
    public Guid SupplierAccountId { get; set; }
    public List<Guid> ProductIds { get; set; } = new();
    public DateTime JoinedAt { get; set; }
    public bool IsActive { get; set; } = true;

    public Campaign Campaign { get; set; } = null!;
}
