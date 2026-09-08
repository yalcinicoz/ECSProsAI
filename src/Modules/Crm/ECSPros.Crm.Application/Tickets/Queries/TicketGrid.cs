using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Tickets.Queries;

/// <summary>
/// Müşteri İlişkileri (talep/şikayet) DataGrid şeması (plan F4): beyaz listeli sıralama/filtre alanları + mevcut adlandırılmış
/// filtrelerin tek yerden uygulanması (liste, sayaçlar ve export AYNI filtre modeli).
/// </summary>
public static class TicketGrid
{
    public static readonly string[] Types = { "complaint", "request" };

    public static readonly GridSchema<Ticket> Schema = new GridSchema<Ticket>()
        .Number("trackingNo", t => t.TrackingNo)
        .Enum("type", t => t.Type, Types)
        .Enum("status", t => t.Status.Code)
        .Text("customer", t => t.CustomerName)
        .Text("customerPhone", t => t.CustomerPhone)
        .Text("caller", t => t.CallerName)
        .Text("callerPhone", t => t.CallerPhone)
        .Text("orderNumber", t => t.OrderNumber)
        .Text("createdByName", t => t.CreatedByName)
        .Date("createdAt", t => t.CreatedAt)
        .Date("lastActivityAt", t => t.LastActivityAt)
        .Number("activityCount", t => t.ActivityCount)
        .Bool("hidden", t => t.IsHidden)
        .Guid("subjectId", t => t.SubjectId)
        .Guid("memberId", t => t.MemberId)
        .Guid("orderId", t => t.OrderId)
        .Guid("firmPlatformId", t => t.FirmPlatformId)
        .Guid("createdBy", t => t.CreatedByUserId)
        .Sort("trackingNo", t => t.TrackingNo)
        .Sort("createdAt", t => t.CreatedAt)
        .Sort("lastActivityAt", t => t.LastActivityAt)
        .Sort("customer", t => t.CustomerName)
        .Sort("activityCount", t => t.ActivityCount)
        .Sort("status", t => t.Status.SortOrder)
        .Sort("type", t => t.Type)
        .DefaultSort(t => t.CreatedAt, desc: true)
        .TieBreaker(t => t.Id);

    /// <summary>Mevcut adlandırılmış filtreler (eski istemciler/derin linkler bozulmaz). Durum filtresi ayrı (sayaçlar durum hariç).</summary>
    public static IQueryable<Ticket> ApplyNamed(IQueryable<Ticket> q, TicketListFilters f, Guid userId, ICrmDbContext db)
    {
        if (!f.IncludeHidden) q = q.Where(t => !t.IsHidden);
        if (f.Type is not null) q = q.Where(t => t.Type == f.Type);
        if (f.SubjectId is not null) q = q.Where(t => t.SubjectId == f.SubjectId);
        if (f.CreatedBy is not null) q = q.Where(t => t.CreatedByUserId == f.CreatedBy);
        if (f.From is not null) q = q.Where(t => t.CreatedAt >= DateTime.SpecifyKind(f.From.Value, DateTimeKind.Utc));
        if (f.To is not null) q = q.Where(t => t.CreatedAt < DateTime.SpecifyKind(f.To.Value, DateTimeKind.Utc).AddDays(1));
        if (f.TrackingNo is not null) q = q.Where(t => t.TrackingNo == f.TrackingNo);
        if (f.MemberId is not null) q = q.Where(t => t.MemberId == f.MemberId);
        if (f.OrderId is not null) q = q.Where(t => t.OrderId == f.OrderId);
        if (!string.IsNullOrWhiteSpace(f.OrderNumber)) { var on = f.OrderNumber.Trim(); q = q.Where(t => t.OrderNumber == on); }
        if (!string.IsNullOrWhiteSpace(f.Customer))
        {
            var c = f.Customer.Trim();
            var digits = new string(c.Where(char.IsDigit).ToArray());
            q = digits.Length >= 4
                ? q.Where(t => t.CustomerPhone.Contains(digits) || t.CallerPhone.Contains(digits))
                : q.Where(t => t.CustomerName.ToLower().Contains(c.ToLower()) || t.CallerName.ToLower().Contains(c.ToLower()));
        }
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var sl = f.Search.Trim().ToLower();
            q = q.Where(t => t.BodyText.ToLower().Contains(sl) || t.Activities.Any(a => a.BodyText.ToLower().Contains(sl)));
        }
        if (f.TaggedMe) q = q.Where(t => t.Activities.Any(a => a.TaggedUserId == userId));
        if (f.UnreadByMe) q = q.Where(t => t.LastActivityId != null && !db.TicketReads.Any(x => x.ActivityId == t.LastActivityId && x.UserId == userId));
        return q;
    }

    /// <summary>Durum süzgeci: verilirse o durum (gizli olsa da); yoksa gizli olmayan durumlar (K6).</summary>
    public static IQueryable<Ticket> ApplyStatus(IQueryable<Ticket> q, string? statusCode)
        => statusCode is not null ? q.Where(t => t.Status.Code == statusCode) : q.Where(t => !t.Status.IsHidden);

    /// <summary>Adlandırılmış + grid filtreleri (durum hariç → sayaçlar; includeStatus ile durum da).</summary>
    public static IQueryable<Ticket> ApplyAll(IQueryable<Ticket> q, TicketListFilters f, Guid userId, ICrmDbContext db, GridRequest? grid, bool includeStatus)
    {
        q = ApplyNamed(q, f, userId, db);
        // "Kontrol" (readByMe) kullanıcıya bağlı hesaplanır → şemada değil, burada özel: true = son işlemi okudum (ya da işlem yok)
        var readByMe = grid?.Filters.FirstOrDefault(x => string.Equals(x.Field, "readByMe", StringComparison.OrdinalIgnoreCase));
        if (readByMe is not null && bool.TryParse(readByMe.Value, out var okundu))
            q = okundu
                ? q.Where(t => t.LastActivityId == null || db.TicketReads.Any(x => x.ActivityId == t.LastActivityId && x.UserId == userId))
                : q.Where(t => t.LastActivityId != null && !db.TicketReads.Any(x => x.ActivityId == t.LastActivityId && x.UserId == userId));
        q = includeStatus ? Schema.ApplyFilters(q, grid, "readByMe") : Schema.ApplyFilters(q, grid, "status", "readByMe");
        // f.status verilmişse adlandırılmış durum kuralı (gizli olmayanlar) uygulanmaz — grid filtresi kazanır
        if (includeStatus && !(grid?.HasFilter("status") ?? false)) q = ApplyStatus(q, f.StatusCode);
        return q;
    }

    /// <summary>Eski sıralama sözlüğü ("created_asc" | "activity" | varsayılan) — grid sort verilmemişse.</summary>
    public static IQueryable<Ticket> ApplySortCompat(IQueryable<Ticket> q, GridRequest? grid, string? legacySort)
    {
        if (grid?.Sort is { Length: > 0 }) return Schema.ApplySort(q, grid);
        return legacySort switch
        {
            "created_asc" => q.OrderBy(t => t.CreatedAt).ThenBy(t => t.Id),
            "activity" => q.OrderByDescending(t => t.LastActivityAt).ThenByDescending(t => t.Id),
            _ => q.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id),
        };
    }

    public static string TypeLabel(string t) => t switch { "complaint" => "Şikayet", "request" => "Talep", _ => t };
}

/// <summary>Talep listesinin adlandırılmış filtreleri (GET parametreleri / export gövdesi "named").</summary>
public record TicketListFilters(
    string? StatusCode = null, string? Type = null, Guid? SubjectId = null, Guid? CreatedBy = null,
    DateTime? From = null, DateTime? To = null, long? TrackingNo = null,
    string? Customer = null, string? OrderNumber = null, string? Search = null,
    Guid? MemberId = null, Guid? OrderId = null,
    bool TaggedMe = false, bool UnreadByMe = false, bool IncludeHidden = false);

public record TicketExportRow(
    long TrackingNo, string Type, string SubjectName, string StatusName, string CustomerName, string CustomerPhone,
    string CallerName, string CallerPhone, string? OrderNumber, string CreatedByName, DateTime CreatedAt, string? UpdatedByName,
    DateTime LastActivityAt, int ActivityCount, bool IsHidden, string BodyText);

public record ExportTicketsQuery(Guid UserId, TicketListFilters Filters, GridRequest Grid, int MaxRows, string? LegacySort = null)
    : IRequest<Result<GridExportSource<TicketExportRow>>>;

public class ExportTicketsQueryHandler(ICrmDbContext db) : IRequestHandler<ExportTicketsQuery, Result<GridExportSource<TicketExportRow>>>
{
    public async Task<Result<GridExportSource<TicketExportRow>>> Handle(ExportTicketsQuery r, CancellationToken ct)
    {
        var q = TicketGrid.ApplyAll(db.Tickets.AsNoTracking(), r.Filters, r.UserId, db, r.Grid, includeStatus: true);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<TicketExportRow>>($"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = TicketGrid.ApplySortCompat(q, r.Grid, r.LegacySort).Select(t => new TicketExportRow(
            t.TrackingNo, t.Type, t.Subject.Name, t.Status.Name, t.CustomerName, t.CustomerPhone, t.CallerName, t.CallerPhone,
            t.OrderNumber, t.CreatedByName, t.CreatedAt, t.UpdatedByName, t.LastActivityAt, t.ActivityCount, t.IsHidden, t.BodyText));
        return Result.Success(new GridExportSource<TicketExportRow>(count, rows));
    }
}
