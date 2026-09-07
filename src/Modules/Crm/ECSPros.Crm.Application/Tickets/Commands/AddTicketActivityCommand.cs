using ECSPros.Crm.Application.Helpers;
using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Tickets.Commands;

/// <summary>
/// Yeni işlem (eski MusteriIliskileriYonetimiDetayInsert): içerik + durum + (tek) etiketlenen personel. Kaydın durumu,
/// güncelleyeni ve son işlemi güncellenir. İşlemi yapan kendi işlemini okumuş sayılır. Bildirimler (K2, eski kurallar):
/// kaydı açan (işlemi yapan değilse) · etiketlenen · etiketleme YOKSA daha önce etiketlenip hiç işlem yapmamış olanlar ·
/// kayda daha önce işlem yapmış diğer personel. Yeni kayıt açılışında bildirim üretilmez.
/// </summary>
public record AddTicketActivityCommand(
    Guid TicketId, string? BodyHtml, List<string>? Attachments, Guid StatusId,
    Guid? TaggedUserId, string? TaggedUserName,
    Guid UserId, string UserName) : IRequest<Result<TicketActivityResultDto>>;

public class AddTicketActivityCommandHandler(ICrmDbContext db) : IRequestHandler<AddTicketActivityCommand, Result<TicketActivityResultDto>>
{
    public async Task<Result<TicketActivityResultDto>> Handle(AddTicketActivityCommand r, CancellationToken ct)
    {
        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == r.TicketId, ct);
        if (ticket is null) return Result.Failure<TicketActivityResultDto>("Kayıt bulunamadı.");
        var status = await db.TicketStatuses.FirstOrDefaultAsync(s => s.Id == r.StatusId, ct);
        if (status is null) return Result.Failure<TicketActivityResultDto>("Durum bulunamadı.");
        if (status.IsHidden && status.Id != ticket.StatusId) return Result.Failure<TicketActivityResultDto>("Bu durum seçilemez.");

        var bodyHtml = HtmlTemizleyici.Temizle(r.BodyHtml);
        var bodyText = HtmlTemizleyici.DuzMetin(bodyHtml);
        var ekler = (r.Attachments ?? []).Where(a => !string.IsNullOrWhiteSpace(a)).Distinct().ToList();
        if (bodyText.Length == 0 && ekler.Count == 0) return Result.Failure<TicketActivityResultDto>("İşlem içeriği zorunludur.");
        if (r.TaggedUserId == r.UserId) return Result.Failure<TicketActivityResultDto>("Kendinizi etiketleyemezsiniz.");

        var now = DateTime.UtcNow;
        var oncekiDurum = ticket.StatusId;
        var activity = new TicketActivity
        {
            TicketId = ticket.Id, BodyHtml = bodyHtml, BodyText = bodyText, Attachments = ekler,
            UserId = r.UserId, UserName = r.UserName, CreatedBy = r.UserId,
            StatusId = status.Id, PreviousStatusId = oncekiDurum == status.Id ? null : oncekiDurum,
            TaggedUserId = r.TaggedUserId, TaggedUserName = r.TaggedUserId is null ? null : r.TaggedUserName,
        };
        db.TicketActivities.Add(activity);
        // işlemi yapan kendi işlemini okumuş sayılır
        db.TicketReads.Add(new TicketRead { TicketId = ticket.Id, ActivityId = activity.Id, UserId = r.UserId, UserName = r.UserName, CreatedBy = r.UserId });

        ticket.StatusId = status.Id;
        ticket.UpdatedByUserId = r.UserId; ticket.UpdatedByName = r.UserName; ticket.UpdatedBy = r.UserId;
        ticket.LastActivityAt = now; ticket.LastActivityId = activity.Id; ticket.ActivityCount += 1;

        // ── bildirimler ──
        var pushes = new List<TicketNotificationPushDto>();
        void Bildir(Guid userId, string kind, string message)
        {
            if (userId == r.UserId || pushes.Any(p => p.UserId == userId)) return;
            var n = new TicketNotification { UserId = userId, TicketId = ticket.Id, TrackingNo = ticket.TrackingNo, ActivityId = activity.Id, Kind = kind, Message = message };
            db.TicketNotifications.Add(n);
            pushes.Add(new TicketNotificationPushDto(n.Id, userId, kind, message));
        }
        var tn = ticket.TrackingNo;
        var oncekiIslemler = await db.TicketActivities.Where(a => a.TicketId == ticket.Id && a.Id != activity.Id)
            .Select(a => new { a.UserId, a.TaggedUserId }).ToListAsync(ct);
        var islemYapanlar = oncekiIslemler.Where(a => a.UserId != null).Select(a => a.UserId!.Value).ToHashSet();

        if (r.TaggedUserId is { } tagged)
            Bildir(tagged, "tagged", $"{tn} takip nolu Müşteri İlişkileri kaydına etiketlendiniz.");
        if (ticket.CreatedByUserId is { } creator)
            Bildir(creator, "created_by", $"{tn} takip nolu oluşturduğunuz Müşteri İlişkileri kaydınıza yeni işlem yapıldı.");
        if (r.TaggedUserId is null)
            foreach (var q in oncekiIslemler.Where(a => a.TaggedUserId != null).Select(a => a.TaggedUserId!.Value).Distinct().Where(u => !islemYapanlar.Contains(u)))
                Bildir(q, "tagged_followup", $"{tn} takip nolu etiketlendiğiniz Müşteri İlişkileri kaydına yeni işlem yapıldı.");
        foreach (var p in islemYapanlar)
            Bildir(p, "participant", $"{tn} takip nolu işlem yaptığınız Müşteri İlişkileri kaydına yeni işlem yapıldı.");

        await db.SaveChangesAsync(ct);
        return Result.Success(new TicketActivityResultDto(activity.Id, ticket.TrackingNo, pushes));
    }
}
