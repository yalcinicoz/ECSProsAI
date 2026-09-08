using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Tickets.Queries;

/// <summary>
/// Liste (eski /crm/musteri-iliskileri-yonetimi). Varsayılan: gizli olmayan kayıtlar + gizli olmayan durumlar (K6);
/// StatusCode verilirse o durum (gizli olsa da) listelenir; IncludeHidden ile gizlenen kayıtlar. Sayaçlar aynı süzgeç
/// (durum hariç) üzerinden hesaplanır. ReadByMe = son işlem okunmuş mu (eski "Kontrol Edildi").
/// </summary>
public record GetTicketsQuery(
    Guid UserId, int Page = 1, int PageSize = 20,
    string? StatusCode = null, string? Type = null, Guid? SubjectId = null, Guid? CreatedBy = null,
    DateTime? From = null, DateTime? To = null, long? TrackingNo = null,
    string? Customer = null, string? OrderNumber = null, string? Search = null,
    Guid? MemberId = null, Guid? OrderId = null,
    bool TaggedMe = false, bool UnreadByMe = false, bool IncludeHidden = false,
    string? Sort = null,
    GridRequest? Grid = null) : IRequest<Result<TicketPageDto>>;
    // Grid (2026-09-08, DataGrid F4): beyaz listeli f.* filtreleri + sort/dir (TicketGrid.Schema); null → eski davranış

public record TicketPageDto(List<TicketListItemDto> Items, int TotalCount, int Page, int PageSize, List<TicketCounterDto> Counters);

public class GetTicketsQueryHandler(ICrmDbContext db) : IRequestHandler<GetTicketsQuery, Result<TicketPageDto>>
{
    public async Task<Result<TicketPageDto>> Handle(GetTicketsQuery r, CancellationToken ct)
    {
        // DataGrid F4 (2026-09-08): adlandırılmış filtreler + beyaz listeli grid filtreleri TEK yerden (TicketGrid).
        var filters = new TicketListFilters(r.StatusCode, r.Type, r.SubjectId, r.CreatedBy, r.From, r.To, r.TrackingNo,
            r.Customer, r.OrderNumber, r.Search, r.MemberId, r.OrderId, r.TaggedMe, r.UnreadByMe, r.IncludeHidden);
        var baseQ = db.Tickets.AsNoTracking().Include(t => t.Subject).Include(t => t.Status).AsQueryable();

        // sayaçlar: durum süzgeci hariç aynı küme
        var counterQ = TicketGrid.ApplyAll(baseQ, filters, r.UserId, db, r.Grid, includeStatus: false);
        var counters = await counterQ.GroupBy(t => new { t.Status.Code, t.Status.Name, t.Status.Color, t.Status.SortOrder })
            .Select(g => new { g.Key, N = g.Count() }).OrderBy(x => x.Key.SortOrder)
            .Select(x => new TicketCounterDto(x.Key.Code, x.Key.Name, x.Key.Color, x.N)).ToListAsync(ct);

        var q = TicketGrid.ApplyAll(baseQ, filters, r.UserId, db, r.Grid, includeStatus: true);

        var total = await q.CountAsync(ct);
        q = TicketGrid.ApplySortCompat(q, r.Grid, r.Sort);
        var page = Math.Max(1, r.Page); var size = Math.Clamp(r.PageSize, 1, 200);
        var rows = await q.Skip((page - 1) * size).Take(size).Select(t => new
        {
            t.Id, t.TrackingNo, t.Type, SubjectName = t.Subject.Name, StatusCode = t.Status.Code, StatusName = t.Status.Name, StatusColor = t.Status.Color,
            t.CustomerName, t.CustomerPhone, t.CallerName, t.CallerPhone, t.OrderNumber, t.OrderId, t.MemberId, t.FirmPlatformId,
            t.CreatedByName, t.CreatedAt, t.UpdatedByName, t.LastActivityAt, t.ActivityCount, t.LastActivityId, t.IsHidden,
            ReadByMe = t.LastActivityId == null || db.TicketReads.Any(x => x.ActivityId == t.LastActivityId && x.UserId == r.UserId),
            TaggedMe = t.Activities.Any(a => a.TaggedUserId == r.UserId),
        }).ToListAsync(ct);

        var items = rows.Select(x => new TicketListItemDto(x.Id, x.TrackingNo, x.Type, x.SubjectName, x.StatusCode, x.StatusName, x.StatusColor,
            x.CustomerName, x.CustomerPhone, x.CallerName, x.CallerPhone, x.OrderNumber, x.OrderId, x.MemberId, x.FirmPlatformId,
            x.CreatedByName, x.CreatedAt, x.UpdatedByName, x.LastActivityAt, x.ActivityCount, x.ReadByMe, x.TaggedMe, x.IsHidden)).ToList();
        return Result.Success(new TicketPageDto(items, total, page, size, counters));
    }
}
