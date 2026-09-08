using ECSPros.Integration.Domain.Entities;
using ECSPros.Shared.Kernel.Grid;

namespace ECSPros.Integration.Application.Queries.GetIntegrationLogs;

/// <summary>
/// Takip event outbox'ı DataGrid şeması (Pazarlama → Takip &amp; Reklam; tüm sütunlarda filtre, 2026-09-08). Liste/export ucu
/// TrackingAdminController'da doğrudan DbContext'le çalışır; kanal (firmPlatformId) ve durum sekmesi adlandırılmış parametredir.
/// TargetsJson/PayloadJson jsonb → metin filtresi yok (yalnız "hedefi var" bool).
/// </summary>
public static class TrackingOutboxGrid
{
    public static readonly string[] Statuses = { "pending", "done", "error", "skipped" };
    public static readonly string[] Sources = { "web", "mobile", "server" };

    public static readonly GridSchema<TrackingEventOutbox> Schema = new GridSchema<TrackingEventOutbox>()
        .Date("createdAt", o => o.CreatedAt)
        .Date("occurredAt", o => o.OccurredAt)
        .Date("processedAt", o => o.ProcessedAt)
        .Date("nextAttemptAt", o => o.NextAttemptAt)
        .Text("eventName", o => o.EventName)
        .Text("dedupId", o => o.DedupId)
        .Enum("source", o => o.Source, Sources)
        .Enum("status", o => o.Status, Statuses)
        .Number("attempts", o => o.AttemptCount)
        .Text("lastError", o => o.LastError)
        .Bool("hasError", o => o.LastError != null && o.LastError != "")
        .Bool("hasTargets", o => o.TargetsJson != null)
        .Guid("firmPlatformId", o => o.FirmPlatformId)
        .Sort("createdAt", o => o.CreatedAt)
        .Sort("occurredAt", o => o.OccurredAt)
        .Sort("processedAt", o => o.ProcessedAt)
        .Sort("eventName", o => o.EventName)
        .Sort("source", o => o.Source)
        .Sort("status", o => o.Status)
        .Sort("attempts", o => o.AttemptCount)
        .DefaultSort(o => o.CreatedAt, desc: true)
        .TieBreaker(o => o.Id);

    public static IQueryable<TrackingEventOutbox> ApplyAll(IQueryable<TrackingEventOutbox> q, Guid firmPlatformId, string? status, string? search, GridRequest? grid)
    {
        q = q.Where(o => o.FirmPlatformId == firmPlatformId);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(o => o.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            q = q.Where(o => o.EventName.ToLower().Contains(term) || o.DedupId.ToLower().Contains(term)
                || (o.LastError != null && o.LastError.ToLower().Contains(term)));
        }
        return Schema.ApplyFilters(q, grid);
    }

    public static string StatusLabel(string s) => s switch
    { "pending" => "Bekliyor", "done" => "Gönderildi", "error" => "Hata", "skipped" => "Atlandı", _ => s };
}

public record TrackingOutboxExportRow(
    DateTime CreatedAt, DateTime OccurredAt, string EventName, string DedupId, string Source, string Status, int AttemptCount,
    DateTime? NextAttemptAt, DateTime? ProcessedAt, string? LastError, string? TargetsJson);
