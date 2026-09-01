using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// Satıcıya Soru Sor (2026-09-01, kullanıcı isteği) — ürün detayından üyenin sorduğu soru.
/// ProductReview deseninin sadeleşmiş kardeşi: pending doğar; admin cevaplayınca
/// (answered) ürün detayında HERKESE görünür; hidden = yayından kaldırıldı (üye
/// Hesabım'da cevabı yine görür). MemberName kayıt anındaki MASKELİ ad anlık
/// görüntüsüdür ("E*** K***") — yayında kişisel veri sergilenmez.
/// Not: seller kaynaklı ürünlerde satıcı paneline akıtma ileri iş (v1 admin cevaplar).
/// </summary>
public class ProductQuestion : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public Guid MemberId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string? Answer { get; set; }
    public string Status { get; set; } = "pending"; // pending | answered | hidden
    public string MemberName { get; set; } = string.Empty; // maskeli anlık görüntü
    public DateTime? AnsweredAt { get; set; }
    public Guid? AnsweredBy { get; set; }          // cevaplayan panel kullanıcısı
}
