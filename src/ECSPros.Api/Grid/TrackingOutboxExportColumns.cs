using ECSPros.Integration.Application.Queries.GetIntegrationLogs;

namespace ECSPros.Api.Grid;

/// <summary>Takip event outbox'ı Excel kolonları (zaman kilitli).</summary>
public static class TrackingOutboxExportColumns
{
    private static object? Tarih(DateTime? d) => d.HasValue ? GridExportWriter.ToIstanbul(d.Value) : null;

    public static readonly IReadOnlyList<GridExportColumn<TrackingOutboxExportRow>> All = new GridExportColumn<TrackingOutboxExportRow>[]
    {
        new("createdAt", "Zaman", r => GridExportWriter.ToIstanbul(r.CreatedAt), Locked: true),
        new("occurredAt", "Olay Zamanı", r => GridExportWriter.ToIstanbul(r.OccurredAt)),
        new("eventName", "Event", r => r.EventName),
        new("dedupId", "Dedup Id", r => r.DedupId),
        new("source", "Kaynak", r => r.Source),
        new("status", "Durum", r => TrackingOutboxGrid.StatusLabel(r.Status)),
        new("attempts", "Deneme", r => r.AttemptCount),
        new("nextAttemptAt", "Sonraki Deneme", r => Tarih(r.NextAttemptAt)),
        new("processedAt", "İşlenme", r => Tarih(r.ProcessedAt)),
        new("lastError", "Hata", r => r.LastError),
        new("targets", "Hedefler", r => r.TargetsJson),
    };
}
