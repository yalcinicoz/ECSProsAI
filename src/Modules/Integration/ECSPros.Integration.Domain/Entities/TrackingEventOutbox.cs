using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Integration.Domain.Entities;

/// <summary>
/// Commerce event outbox'ı (İE-2 Faz B, 2026-08-22 — plan docs/reklam-analytics-entegrasyon-is-akisi.md).
/// ICommerceEventPublisher her event'i buraya yazar; TrackingDispatchWorker dilimler halinde
/// okuyup kanalın aktif takip adapter'larına (Meta CAPI/TikTok/GA4 MP — Faz D) dağıtır.
/// Kalıcı kuyruk: restart/deploy event kaybetmez (LegacyOrderOutbox / ful_cargo_notify_outbox kalıbı).
/// Aynı (FirmPlatformId, EventName, DedupId) için TEK satır — purchase dedup'u OrderId üzerinden.
/// </summary>
public class TrackingEventOutbox : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string DedupId { get; set; } = string.Empty;
    public string Source { get; set; } = "web";            // web | mobile | server
    public DateTime OccurredAt { get; set; }
    /// <summary>CommerceEvent JSON (System.Text.Json, camelCase).</summary>
    public string PayloadJson { get; set; } = "{}";
    /// <summary>pending | done | error | skipped (hedef adapter yok / consent yok)</summary>
    public string Status { get; set; } = "pending";
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    public DateTime? ProcessedAt { get; set; }
    /// <summary>Dağıtım sonucu: [{"adapter":"meta","status":"success|failure|dry_run|skipped","error":"..."}]</summary>
    public string? TargetsJson { get; set; }
}
