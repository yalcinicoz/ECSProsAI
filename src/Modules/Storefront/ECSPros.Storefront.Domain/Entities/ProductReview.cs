using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// E7: ürün değerlendirmesi — satın alma şartlı (OrderItemId teslim edilmiş sipariş
/// kalemine işaret eder; doğrulama API katmanında, Order+Catalog verisiyle). Moderasyonlu:
/// pending doğar; kart/detay puan ortalamaları YALNIZ approved yorumlardan hesaplanır.
/// MemberName yayın anındaki maskeli ad anlık görüntüsüdür ("E*** K***").
/// Üyenin sildiği yorum soft-delete olur (Yorumlarım "Silinenler" sekmesi filtresiz okur).
/// Foto ekleme, görsel yükleme altyapısıyla birlikte (E8 iade görseli/H) ele alınacak.
/// </summary>
public class ProductReview : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public Guid MemberId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public Guid? OrderItemId { get; set; }
    public int Rating { get; set; }               // 1-5
    public string? Text { get; set; }
    public string Status { get; set; } = "pending"; // pending | approved | rejected
    public string? RejectReason { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public DateTime? ModeratedAt { get; set; }
}
