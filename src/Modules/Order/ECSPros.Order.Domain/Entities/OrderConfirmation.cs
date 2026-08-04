using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

/// <summary>
/// Sipariş onay linki kaydı (O2, 2026-08-04): SMS/e-posta ile gönderilen onay linkinin
/// token'ı HASH'lenerek saklanır (ham token yalnız linkte yaşar), 24 saat (ayarlanabilir)
/// ömürlüdür. Onay gerçekleşince ConfirmedAt + ConfirmedVia dolar. Yeniden gönderimde
/// eski kayıt pasifleşir (IsDeleted), yeni kayıt açılır.
/// </summary>
public class OrderConfirmation : BaseEntity
{
    public Guid OrderId { get; set; }

    /// <summary>SHA256(token) hex — ham token saklanmaz.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }

    /// <summary>link | site | payment</summary>
    public string? ConfirmedVia { get; set; }

    public DateTime? SmsSentAt { get; set; }
    public DateTime? EmailSentAt { get; set; }
}
