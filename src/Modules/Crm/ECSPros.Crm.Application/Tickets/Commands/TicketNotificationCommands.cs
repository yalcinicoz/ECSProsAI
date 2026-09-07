using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Tickets.Commands;

/// <summary>Bildirim listesi açıldı → listelenen bildirimler "gördü" (SeenAt). Kayda girme ayrı (OpenTicket / MarkOpened).</summary>
public record MarkTicketNotificationsSeenCommand(Guid UserId, List<Guid> Ids) : IRequest<Result<int>>;
public class MarkTicketNotificationsSeenCommandHandler(ICrmDbContext db) : IRequestHandler<MarkTicketNotificationsSeenCommand, Result<int>>
{
    public async Task<Result<int>> Handle(MarkTicketNotificationsSeenCommand r, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var list = await db.TicketNotifications.Where(n => n.UserId == r.UserId && n.SeenAt == null && r.Ids.Contains(n.Id)).ToListAsync(ct);
        foreach (var n in list) n.SeenAt = now;
        if (list.Count > 0) await db.SaveChangesAsync(ct);
        return Result.Success(list.Count);
    }
}

/// <summary>Bildirime tıklandı → "kayda girdi" (OpenedAt). Ardından panel kayda gider ve OpenTicket okundu satırlarını yazar.</summary>
public record MarkTicketNotificationOpenedCommand(Guid UserId, Guid Id) : IRequest<Result<Guid>>;
public class MarkTicketNotificationOpenedCommandHandler(ICrmDbContext db) : IRequestHandler<MarkTicketNotificationOpenedCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(MarkTicketNotificationOpenedCommand r, CancellationToken ct)
    {
        var n = await db.TicketNotifications.FirstOrDefaultAsync(x => x.Id == r.Id && x.UserId == r.UserId, ct);
        if (n is null) return Result.Failure<Guid>("Bildirim bulunamadı.");
        var now = DateTime.UtcNow;
        n.OpenedAt ??= now; n.SeenAt ??= now;
        await db.SaveChangesAsync(ct);
        return Result.Success(n.TicketId);
    }
}
