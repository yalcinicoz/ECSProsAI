using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
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
    string? Sort = null) : IRequest<Result<TicketPageDto>>;

public record TicketPageDto(List<TicketListItemDto> Items, int TotalCount, int Page, int PageSize, List<TicketCounterDto> Counters);

public class GetTicketsQueryHandler(ICrmDbContext db) : IRequestHandler<GetTicketsQuery, Result<TicketPageDto>>
{
    public async Task<Result<TicketPageDto>> Handle(GetTicketsQuery r, CancellationToken ct)
    {
        var q = db.Tickets.AsNoTracking().Include(t => t.Subject).Include(t => t.Status).AsQueryable();
        if (!r.IncludeHidden) q = q.Where(t => !t.IsHidden);
        if (r.Type is not null) q = q.Where(t => t.Type == r.Type);
        if (r.SubjectId is not null) q = q.Where(t => t.SubjectId == r.SubjectId);
        if (r.CreatedBy is not null) q = q.Where(t => t.CreatedByUserId == r.CreatedBy);
        if (r.From is not null) q = q.Where(t => t.CreatedAt >= DateTime.SpecifyKind(r.From.Value, DateTimeKind.Utc));
        if (r.To is not null) q = q.Where(t => t.CreatedAt < DateTime.SpecifyKind(r.To.Value, DateTimeKind.Utc).AddDays(1));
        if (r.TrackingNo is not null) q = q.Where(t => t.TrackingNo == r.TrackingNo);
        if (r.MemberId is not null) q = q.Where(t => t.MemberId == r.MemberId);
        if (r.OrderId is not null) q = q.Where(t => t.OrderId == r.OrderId);
        if (!string.IsNullOrWhiteSpace(r.OrderNumber)) { var on = r.OrderNumber.Trim(); q = q.Where(t => t.OrderNumber == on); }
        if (!string.IsNullOrWhiteSpace(r.Customer))
        {
            var c = r.Customer.Trim();
            var digits = new string(c.Where(char.IsDigit).ToArray());
            q = digits.Length >= 4
                ? q.Where(t => t.CustomerPhone.Contains(digits) || t.CallerPhone.Contains(digits))
                : q.Where(t => t.CustomerName.ToLower().Contains(c.ToLower()) || t.CallerName.ToLower().Contains(c.ToLower()));
        }
        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            var s = r.Search.Trim();
            var sl = s.ToLower();
            q = q.Where(t => t.BodyText.ToLower().Contains(sl) || t.Activities.Any(a => a.BodyText.ToLower().Contains(sl)));
        }
        if (r.TaggedMe) q = q.Where(t => t.Activities.Any(a => a.TaggedUserId == r.UserId));
        if (r.UnreadByMe) q = q.Where(t => t.LastActivityId != null && !db.TicketReads.Any(x => x.ActivityId == t.LastActivityId && x.UserId == r.UserId));

        // sayaçlar: durum süzgeci hariç aynı küme
        var counters = await q.GroupBy(t => new { t.Status.Code, t.Status.Name, t.Status.Color, t.Status.SortOrder })
            .Select(g => new { g.Key, N = g.Count() }).OrderBy(x => x.Key.SortOrder)
            .Select(x => new TicketCounterDto(x.Key.Code, x.Key.Name, x.Key.Color, x.N)).ToListAsync(ct);

        q = r.StatusCode is not null ? q.Where(t => t.Status.Code == r.StatusCode) : q.Where(t => !t.Status.IsHidden);

        var total = await q.CountAsync(ct);
        q = r.Sort switch
        {
            "created_asc" => q.OrderBy(t => t.CreatedAt),
            "activity" => q.OrderByDescending(t => t.LastActivityAt),
            _ => q.OrderByDescending(t => t.CreatedAt),
        };
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
