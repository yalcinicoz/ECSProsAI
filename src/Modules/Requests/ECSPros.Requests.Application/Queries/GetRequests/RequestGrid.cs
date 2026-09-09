using ECSPros.Requests.Application.Services;
using ECSPros.Requests.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Requests.Application.Queries.GetRequests;

/// <summary>
/// Proje talepleri DataGrid şeması (2026-09-09): beyaz listeli filtre/sıralama + mevcut adlandırılmış
/// filtreler (status/category/priority/assignedTo/requestedBy) + global arama (kod, başlık, açıklama).
///
/// <para><c>overdue</c>: termini geçmiş ve hâlâ kapanmamış talep — panelde tek tıkla süzülür.</para>
/// </summary>
public static class RequestGrid
{
    public static readonly string[] Priorities = { "low", "normal", "high", "critical" };
    public static readonly string[] Statuses = { "new", "in_progress", "waiting", "done", "rejected", "cancelled" };

    public static readonly GridSchema<ProjectRequest> Schema = new GridSchema<ProjectRequest>()
        .Text("code", r => r.Code)
        .Text("title", r => r.Title)
        .Text("description", r => r.Description)
        .Text("requestedByName", r => r.RequestedByName)
        .Text("assignedToName", r => r.AssignedToName)
        .Enum("status", r => r.Status, Statuses)
        .Enum("priority", r => r.Priority, Priorities)
        .Enum("category", r => r.Category)
        .Bool("assigned", r => r.AssignedTo != null)
        .Bool("overdue", r => r.DueDate != null && r.CompletedAt == null
            && r.DueDate < DateOnly.FromDateTime(DateTime.UtcNow))
        .Date("dueDate", r => r.DueDate)
        .Date("createdAt", r => r.CreatedAt)
        .Date("completedAt", r => r.CompletedAt)
        .Number("commentCount", r => r.Activities.Count(a => a.ActivityType == "comment"))
        .Guid("assignedTo", r => r.AssignedTo)
        .Guid("requestedBy", r => r.RequestedBy)
        .Sort("code", r => r.Code)
        .Sort("title", r => r.Title)
        .Sort("category", r => r.Category)
        .Sort("priority", r => r.Priority)
        .Sort("status", r => r.Status)
        .Sort("requestedByName", r => r.RequestedByName)
        .Sort("assignedToName", r => r.AssignedToName)
        .Sort("dueDate", r => r.DueDate)
        .Sort("createdAt", r => r.CreatedAt)
        .Sort("completedAt", r => r.CompletedAt)
        .Sort("commentCount", r => r.Activities.Count(a => a.ActivityType == "comment"))
        .DefaultSort(r => r.CreatedAt, desc: true)
        .TieBreaker(r => r.Id);

    public static IQueryable<ProjectRequest> ApplyNamed(
        IQueryable<ProjectRequest> query, RequestListFilters f, bool includeStatus = true)
    {
        if (includeStatus && !string.IsNullOrWhiteSpace(f.Status)) query = query.Where(r => r.Status == f.Status);
        if (!string.IsNullOrWhiteSpace(f.Category)) query = query.Where(r => r.Category == f.Category);
        if (!string.IsNullOrWhiteSpace(f.Priority)) query = query.Where(r => r.Priority == f.Priority);
        if (f.AssignedTo is { } atanan) query = query.Where(r => r.AssignedTo == atanan);
        if (f.RequestedBy is { } eden) query = query.Where(r => r.RequestedBy == eden);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var arama = f.Search.Trim().ToLowerInvariant();
            query = query.Where(r =>
                r.Title.ToLower().Contains(arama) ||
                r.Code.ToLower().Contains(arama) ||
                r.Description.ToLower().Contains(arama));
        }
        return query;
    }

    /// <param name="includeStatus">false → sekme sayaçları için durum filtresi uygulanmaz.</param>
    public static IQueryable<ProjectRequest> ApplyAll(
        IQueryable<ProjectRequest> query, RequestListFilters f, GridRequest? grid, bool includeStatus = true)
    {
        query = ApplyNamed(query, f, includeStatus);
        return includeStatus ? Schema.ApplyFilters(query, grid) : Schema.ApplyFilters(query, grid, "status");
    }

    public static string StatusLabel(string s) => s switch
    {
        "new" => "Yeni", "in_progress" => "Çalışılıyor", "waiting" => "Beklemede",
        "done" => "Tamamlandı", "rejected" => "Reddedildi", "cancelled" => "İptal", _ => s,
    };

    public static string PriorityLabel(string s) => s switch
    {
        "low" => "Düşük", "normal" => "Normal", "high" => "Yüksek", "critical" => "Kritik", _ => s,
    };
}

public record RequestListFilters(
    string? Status = null, string? Category = null, string? Priority = null,
    Guid? AssignedTo = null, Guid? RequestedBy = null, string? Search = null);

public record RequestExportRow(
    string Code, string Title, string Category, string Priority, string Status,
    string RequestedByName, string? AssignedToName, DateOnly? DueDate, DateTime CreatedAt,
    DateTime? CompletedAt, int CommentCount);

public record ExportRequestsQuery(RequestListFilters Filters, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<RequestExportRow>>>;

public class ExportRequestsQueryHandler(IRequestsDbContext db)
    : IRequestHandler<ExportRequestsQuery, Result<GridExportSource<RequestExportRow>>>
{
    public async Task<Result<GridExportSource<RequestExportRow>>> Handle(ExportRequestsQuery r, CancellationToken ct)
    {
        var q = RequestGrid.ApplyAll(db.ProjectRequests.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<RequestExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");

        var rows = RequestGrid.Schema.ApplySort(q, r.Grid).Select(x => new RequestExportRow(
            x.Code, x.Title, x.Category, x.Priority, x.Status, x.RequestedByName, x.AssignedToName,
            x.DueDate, x.CreatedAt, x.CompletedAt, x.Activities.Count(a => a.ActivityType == "comment")));
        return Result.Success(new GridExportSource<RequestExportRow>(count, rows));
    }
}
