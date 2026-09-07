using ECSPros.Crm.Application.Helpers;
using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Tickets.Commands;

/// <summary>
/// Yeni kayıt (eski MusteriIliskileriYonetimiInsert). Tür konudan türer; konunun zorunlu alanları doğrulanır;
/// aynı sipariş + aynı konu için açık (muaf olmayan durumda) kayıt varsa mükerrer hatası. Sipariş/müşteri bilgileri
/// çağıran (API) tarafından sipariş aramasıyla çözülüp anlık görüntü olarak gelir (K9 kanal seçimi orada).
/// Takip no = unix saniye + sıra (K4); benzersizlik DB indeksiyle, çakışmada bir sonraki sıra denenir.
/// </summary>
public record CreateTicketCommand(
    Guid SubjectId,
    string? CallerName, string? CallerPhone,
    string? OrderNumber, Guid? OrderId, Guid? FirmPlatformId,
    Guid? MemberId, int? LegacyMemberId, string? CustomerName, string? CustomerPhone,
    string? BodyHtml, List<string>? Attachments,
    Guid? UserId, string UserName) : IRequest<Result<CreateTicketResult>>;

public record CreateTicketResult(Guid Id, long TrackingNo);

public class CreateTicketCommandHandler(ICrmDbContext db) : IRequestHandler<CreateTicketCommand, Result<CreateTicketResult>>
{
    public async Task<Result<CreateTicketResult>> Handle(CreateTicketCommand r, CancellationToken ct)
    {
        var subject = await db.TicketSubjects.FirstOrDefaultAsync(s => s.Id == r.SubjectId && s.IsActive, ct);
        if (subject is null) return Result.Failure<CreateTicketResult>("Konu başlığı bulunamadı.");
        var status = await db.TicketStatuses.Where(s => !s.IsHidden).OrderByDescending(s => s.IsDefault).ThenBy(s => s.SortOrder).FirstOrDefaultAsync(ct);
        if (status is null) return Result.Failure<CreateTicketResult>("Varsayılan kayıt durumu tanımlı değil (Ayarlar › Müşteri İlişkileri).");

        var callerName = (r.CallerName ?? "").Trim();
        var callerPhone = TelefonTemizle(r.CallerPhone);
        var orderNumber = (r.OrderNumber ?? "").Trim();
        var bodyHtml = HtmlTemizleyici.Temizle(r.BodyHtml);
        var bodyText = HtmlTemizleyici.DuzMetin(bodyHtml);
        var ekler = (r.Attachments ?? []).Where(a => !string.IsNullOrWhiteSpace(a)).Distinct().ToList();

        // konunun zorunlu alanları (eski cm_crm_konu_basliklari_alanlar)
        var hatalar = new List<string>();
        foreach (var f in subject.RequiredFields)
            switch (f)
            {
                case "orderNumber": if (orderNumber.Length == 0) hatalar.Add("Sipariş numarası girilmesi zorunludur."); break;
                case "callerName": if (callerName.Length == 0) hatalar.Add("Arayan ad soyad zorunludur."); break;
                case "callerPhone": if (callerPhone.Length < 10) hatalar.Add("Arayan telefonu (10 hane) zorunludur."); break;
                case "body": if (bodyText.Length == 0) hatalar.Add("İçerik zorunludur."); break;
                case "image": if (ekler.Count == 0) hatalar.Add("Bu konu için en az bir görsel eklenmesi zorunludur."); break;
            }
        if (bodyText.Length == 0 && ekler.Count == 0) hatalar.Add("İçerik ya da ek gönderilmelidir.");
        if (hatalar.Count > 0) return Result.Failure<CreateTicketResult>(string.Join(" ", hatalar.Distinct()));

        // mükerrer: aynı sipariş + aynı konu, muaf olmayan durumda, gizli olmayan kayıt
        if (orderNumber.Length > 0)
        {
            var mevcut = await db.Tickets
                .Where(t => !t.IsHidden && t.SubjectId == subject.Id && t.OrderNumber == orderNumber && !t.Status.ExemptFromDuplicateCheck)
                .Select(t => t.TrackingNo).FirstOrDefaultAsync(ct);
            if (mevcut != 0)
                return Result.Failure<CreateTicketResult>($"Bu sipariş ve konu ile daha önce kayıt açılmış. Takip No: {mevcut}");
        }

        var now = DateTime.UtcNow;
        var ticket = new Ticket
        {
            Type = subject.Type, SubjectId = subject.Id, StatusId = status.Id,
            MemberId = r.MemberId, LegacyMemberId = r.LegacyMemberId,
            CustomerName = (r.CustomerName ?? "").Trim(), CustomerPhone = TelefonTemizle(r.CustomerPhone),
            CallerName = callerName, CallerPhone = callerPhone,
            OrderId = r.OrderId, OrderNumber = orderNumber.Length > 0 ? orderNumber : null, FirmPlatformId = r.FirmPlatformId,
            BodyHtml = bodyHtml, BodyText = bodyText, Attachments = ekler,
            CreatedByUserId = r.UserId, CreatedByName = r.UserName, CreatedBy = r.UserId,
            LastActivityAt = now, ActivityCount = 0,
        };

        // takip no: unix saniye + sıra; benzersizlik indeksine takılırsa sonraki sıra
        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        for (var deneme = 0; deneme < 5; deneme++)
        {
            var seq = await db.NextTicketSequenceAsync(ct);
            var aday = unix + seq;
            if (!await db.Tickets.IgnoreQueryFilters().AnyAsync(t => t.TrackingNo == aday, ct)) { ticket.TrackingNo = aday; break; }
        }
        if (ticket.TrackingNo == 0) return Result.Failure<CreateTicketResult>("Takip numarası üretilemedi, tekrar deneyin.");

        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(ct);
        return Result.Success(new CreateTicketResult(ticket.Id, ticket.TrackingNo));
    }

    /// <summary>Eski kural: rakam dışı atılır, son 10 hane alınır (0 5xx … → 5xx …).</summary>
    public static string TelefonTemizle(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var d = new string(s.Where(char.IsDigit).ToArray());
        return d.Length > 10 ? d[^10..] : d;
    }
}
