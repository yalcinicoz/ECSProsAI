using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Integration.Domain.Entities;

/// <summary>
/// Eski sisteme sipariş senkron kuyruğu (F1, 2026-08-04 — plan:
/// docs/eski-sistem-siparis-senkron-plani.md). Kapıda ödemeli sipariş oluştuğunda,
/// kart siparişi ödeme alınıp onaylandığında 'create' işi kuyruğa düşer; LegacySyncWorker
/// dilimi işler. Checkout/onay akışı kuyruğa yazım başarısız olsa bile ASLA bozulmaz.
/// </summary>
public class LegacyOrderOutbox : BaseEntity
{
    public Guid OrderId { get; set; }

    /// <summary>create | cancel</summary>
    public string JobType { get; set; } = "create";

    /// <summary>pending | dry_run (plan üretildi, gerçek yazım bekliyor) | done | error</summary>
    public string Status { get; set; } = "pending";

    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
