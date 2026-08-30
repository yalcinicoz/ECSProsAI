using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Integration.Domain.Entities;

/// <summary>
/// FAZ 10 / A6 + FAZ 11 / K0 — kalıcı feed üretim kuyruğu. Worker satırı silmez;
/// atomik lease ile sahiplenir. Process/VM lease sırasında kapanırsa süre dolunca
/// başka worker işi geri alır. Tamamlanan ve kalıcı hata alan işler tanı için saklanır.
/// </summary>
public class FeedJob : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public DateTime RequestedAt { get; set; }
    public string Status { get; set; } = FeedJobStatuses.Pending;
    public string? LeaseOwner { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? LastError { get; set; }
}

public static class FeedJobStatuses
{
    public const string Pending = "pending";
    public const string Processing = "processing";
    public const string Completed = "completed";
    public const string Failed = "failed";
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
