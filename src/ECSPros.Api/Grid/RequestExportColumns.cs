using ECSPros.Requests.Application.Queries.GetRequests;

namespace ECSPros.Api.Grid;

/// <summary>Proje talepleri Excel kolonları (kod kilitli).</summary>
public static class RequestExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<RequestExportRow>> All = new GridExportColumn<RequestExportRow>[]
    {
        new("code", "Kod", r => r.Code, Locked: true),
        new("title", "Başlık", r => r.Title),
        new("category", "Kategori", r => r.Category),
        new("priority", "Öncelik", r => RequestGrid.PriorityLabel(r.Priority)),
        new("status", "Durum", r => RequestGrid.StatusLabel(r.Status)),
        new("requestedByName", "Talep Eden", r => r.RequestedByName),
        new("assignedToName", "Atanan", r => r.AssignedToName),
        new("dueDate", "Termin", r => r.DueDate.HasValue ? (object)r.DueDate.Value.ToDateTime(TimeOnly.MinValue) : null),
        new("commentCount", "Yorum", r => r.CommentCount),
        new("createdAt", "Oluşturma", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
        new("completedAt", "Kapanış", r => r.CompletedAt.HasValue ? GridExportWriter.ToIstanbul(r.CompletedAt.Value) : null),
    };
}
