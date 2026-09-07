using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Tickets.Commands;

/// <summary>
/// Kullanıcı kaydı açtı: o ana kadarki her işlem için okundu satırı (eski kontrol_edenler) + bu kayda ait bildirimleri
/// "kayda girdi" (OpenedAt; görülmediyse SeenAt de) işaretle. Detay GET'i yan etkisiz kalsın diye panel bunu ayrı POST ile çağırır.
/// </summary>
public record OpenTicketCommand(Guid TicketId, Guid UserId, string UserName) : IRequest<Result<int>>;

public class OpenTicketCommandHandler(ICrmDbContext db) : IRequestHandler<OpenTicketCommand, Result<int>>
{
    public async Task<Result<int>> Handle(OpenTicketCommand r, CancellationToken ct)
    {
        var okunmus = await db.TicketReads.Where(x => x.TicketId == r.TicketId && x.UserId == r.UserId).Select(x => x.ActivityId).ToListAsync(ct);
        var eksik = await db.TicketActivities.Where(a => a.TicketId == r.TicketId && !okunmus.Contains(a.Id)).Select(a => a.Id).ToListAsync(ct);
        foreach (var id in eksik)
            db.TicketReads.Add(new TicketRead { TicketId = r.TicketId, ActivityId = id, UserId = r.UserId, UserName = r.UserName, CreatedBy = r.UserId });
        var now = DateTime.UtcNow;
        var bildirimler = await db.TicketNotifications.Where(n => n.TicketId == r.TicketId && n.UserId == r.UserId && n.OpenedAt == null).ToListAsync(ct);
        foreach (var n in bildirimler) { n.OpenedAt = now; n.SeenAt ??= now; }
        if (eksik.Count > 0 || bildirimler.Count > 0) await db.SaveChangesAsync(ct);
        return Result.Success(eksik.Count);
    }
}
