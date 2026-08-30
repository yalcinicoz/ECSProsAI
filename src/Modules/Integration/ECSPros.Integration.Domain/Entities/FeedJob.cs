using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Integration.Domain.Entities;

/// <summary>
/// FAZ 10 / A6 — feed üretim iş kuyruğu (integration.feed_jobs). Panel "Şimdi üret"
/// tetiği süreç-içi Channel yerine buraya satır ekler; FeedGeneratorWorker (yalnız
/// Worker/Both rollü düğümde) satırı FOR UPDATE SKIP LOCKED ile sahiplenip SİLER —
/// tetik hangi düğümden gelirse gelsin işi worker düğümü yapar, worker kapalıyken
/// tetik kaybolmaz (DB'de bekler).
/// </summary>
public class FeedJob : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public DateTime RequestedAt { get; set; }
}

/// <summary>
/// FAZ 10 / A6 — kanal başına son feed üretim durumu (integration.feed_status; eski
/// status.json + süreç-içi FeedStatusStore'un yerine). Panel feed kartı bu satırdan
/// okur — üretimi hangi düğüm yaptıysa yapsın her düğüm aynı durumu görür.
/// Kanal başına TEK satır (FirmPlatformId unique, upsert).
/// </summary>
public class FeedRunStatus : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public string PlatformCode { get; set; } = string.Empty;
    public DateTime? LastRunAt { get; set; }
    public int DurationMs { get; set; }
    public int ProductCount { get; set; }
    public int ItemCount { get; set; }
    public int InStockCount { get; set; }
    public long XmlBytes { get; set; }
    public long CsvBytes { get; set; }
    public string? Error { get; set; }
    public bool Running { get; set; }
    /// <summary>Üretimi yapan düğüm (Node:Id) — çoklu sunucuda tanı kolaylığı.</summary>
    public string? NodeId { get; set; }
}
