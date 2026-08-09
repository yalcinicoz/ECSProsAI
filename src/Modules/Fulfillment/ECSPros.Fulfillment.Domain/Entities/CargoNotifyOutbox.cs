using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Fulfillment.Domain.Entities;

/// <summary>
/// OP5 (K-10): kargo bildirimi outbox'ı — paket kapanışında kuyruğa düşer, worker taşıyıcı
/// API'sine asenkron gönderir (masa/yazıcı API yanıtını beklemez; hata retry'lanır).
/// Legacy sipariş senkronundaki outbox kalıbının aynısı. 21:00 fiziki teslim mutabakatı
/// (KG planı) bu kuyruğun üstünde koşacak.
/// </summary>
public class CargoNotifyOutbox : BaseEntity
{
    public Guid PackageId { get; set; }
    public Guid OrderId { get; set; }
    public Guid FirmPlatformId { get; set; }
    public Guid? ShipmentId { get; set; }

    /// <summary>Hedef taşıyıcı (core_firm_platform_integrations kaydı) — yönlendirmeyle değişebilir (K-9).</summary>
    public Guid? CargoIntegrationId { get; set; }
    public string? CargoName { get; set; }

    /// <summary>pending | sent | failed | cancelled</summary>
    public string Status { get; set; } = "pending";

    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? SentAt { get; set; }
}
