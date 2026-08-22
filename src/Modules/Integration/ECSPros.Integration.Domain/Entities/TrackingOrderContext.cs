using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Integration.Domain.Entities;

/// <summary>
/// Sipariş anındaki tarayıcı bağlamı + consent (İE-2 Faz B-3). Checkout başarıyla bitince
/// istekten okunan fbp/fbc/_ga/ttclid/gclid/IP/UA ve ms_consent çerezi buraya yazılır;
/// sipariş daha SONRA onaylanınca (kart onay linki, havale, kapıda ödeme onayı) server-side
/// purchase event'i bu bağlamla üretilir — aksi halde eşleşme kalitesi düşer ve consent
/// bilinmezdi. 90 gün sonra worker temizler. Sipariş başına tek kayıt.
/// </summary>
public class TrackingOrderContext : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid FirmPlatformId { get; set; }
    /// <summary>ClientContext JSON (PII yalnız hash'li).</summary>
    public string ContextJson { get; set; } = "{}";
    /// <summary>ConsentState JSON.</summary>
    public string ConsentJson { get; set; } = "{}";
}
