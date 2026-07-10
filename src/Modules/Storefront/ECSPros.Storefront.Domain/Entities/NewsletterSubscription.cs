using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// F4: footer bülten aboneliği. E-posta platform başına bir kez (kayıt idempotent —
/// tekrar kaydolma başarı döner, soft-silinen geri açılır). Gönderim/kampanya
/// entegrasyonu ileri iş; bu tablo abone listesinin kaynağıdır.
/// </summary>
public class NewsletterSubscription : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public string Email { get; set; } = string.Empty;
    public Guid? MemberId { get; set; }
    public bool IsActive { get; set; } = true;
}
