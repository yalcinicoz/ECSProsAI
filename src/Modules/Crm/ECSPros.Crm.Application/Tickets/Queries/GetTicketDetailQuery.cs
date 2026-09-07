using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Tickets.Queries;

/// <summary>Detay (takip no ile): kayıt + işlemler (okundu listesiyle) + bildirim izi (kim gördü / kayda girdi) + müşterinin diğer kayıtları. Yan etkisiz; okundu yazımı OpenTicketCommand ile.</summary>
public record GetTicketDetailQuery(long TrackingNo) : IRequest<Result<TicketDetailDto>>;

public class GetTicketDetailQueryHandler(ICrmDbContext db) : IRequestHandler<GetTicketDetailQuery, Result<TicketDetailDto>>
{
    public async Task<Result<TicketDetailDto>> Handle(GetTicketDetailQuery r, CancellationToken ct)
    {
        var t = await db.Tickets.AsNoTracking().Include(x => x.Subject).Include(x => x.Status).FirstOrDefaultAsync(x => x.TrackingNo == r.TrackingNo, ct);
        if (t is null) return Result.Failure<TicketDetailDto>("Kayıt bulunamadı.");

        var statuses = await db.TicketStatuses.AsNoTracking().ToDictionaryAsync(s => s.Id, ct);
        var acts = await db.TicketActivities.AsNoTracking().Where(a => a.TicketId == t.Id).OrderBy(a => a.CreatedAt).ToListAsync(ct);
        var reads = await db.TicketReads.AsNoTracking().Where(x => x.TicketId == t.Id).OrderBy(x => x.CreatedAt).ToListAsync(ct);
        var readsByAct = reads.GroupBy(x => x.ActivityId).ToDictionary(g => g.Key, g => g.Select(x => new TicketReadDto(x.UserId, x.UserName, x.CreatedAt)).ToList());
        var notes = await db.TicketNotifications.AsNoTracking().Where(n => n.TicketId == t.Id).OrderByDescending(n => n.CreatedAt).Take(200)
            .Select(n => new TicketNotificationTraceDto(n.UserId, n.Kind, n.Message, n.CreatedAt, n.SeenAt, n.OpenedAt)).ToListAsync(ct);

        List<TicketBriefDto> previous = [];
        if (t.MemberId is not null || t.LegacyMemberId is not null || t.CustomerPhone.Length >= 10)
        {
            var pq = db.Tickets.AsNoTracking().Where(x => x.Id != t.Id && !x.IsHidden);
            pq = t.MemberId is not null ? pq.Where(x => x.MemberId == t.MemberId)
               : t.LegacyMemberId is not null ? pq.Where(x => x.LegacyMemberId == t.LegacyMemberId)
               : pq.Where(x => x.CustomerPhone == t.CustomerPhone);
            previous = await pq.OrderByDescending(x => x.CreatedAt).Take(50)
                .Select(x => new TicketBriefDto(x.Id, x.TrackingNo, x.Subject.Name, x.Status.Name, x.Status.Color, x.CreatedAt, x.CreatedByName)).ToListAsync(ct);
        }

        string StatusName(Guid id) => statuses.TryGetValue(id, out var s) ? s.Name : "";
        var dto = new TicketDetailDto(t.Id, t.TrackingNo, t.Type, t.SubjectId, t.Subject.Name, t.StatusId, t.Status.Code, t.Status.Name, t.Status.Color, t.Status.IsResolved,
            t.MemberId, t.LegacyMemberId, t.CustomerName, t.CustomerPhone, t.CallerName, t.CallerPhone, t.OrderId, t.OrderNumber, t.FirmPlatformId,
            t.BodyHtml, t.Attachments, t.CreatedByUserId, t.CreatedByName, t.CreatedAt, t.UpdatedByName, t.UpdatedAt, t.LastActivityAt, t.IsHidden, t.LegacyId,
            acts.Select(a => new TicketActivityDto(a.Id, a.BodyHtml, a.Attachments, a.UserId, a.UserName, a.CreatedAt,
                StatusName(a.StatusId), a.PreviousStatusId is null ? null : StatusName(a.PreviousStatusId.Value), a.PreviousStatusId is not null,
                statuses.TryGetValue(a.StatusId, out var st) && st.IsResolved, a.TaggedUserId, a.TaggedUserName,
                readsByAct.GetValueOrDefault(a.Id) ?? [])).ToList(),
            notes, previous);
        return Result.Success(dto);
    }
}
