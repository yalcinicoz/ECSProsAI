using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Tickets.Queries;

/// <summary>Kullanıcının bildirimleri; PendingCount = görülmemiş YA DA kayda girilmemiş (eski rozet kuralı: ikisi de olana dek yanar).</summary>
public record GetMyTicketNotificationsQuery(Guid UserId, int Page = 1, int PageSize = 20, bool OnlyPending = false) : IRequest<Result<TicketNotificationPageDto>>;

public class GetMyTicketNotificationsQueryHandler(ICrmDbContext db) : IRequestHandler<GetMyTicketNotificationsQuery, Result<TicketNotificationPageDto>>
{
    public async Task<Result<TicketNotificationPageDto>> Handle(GetMyTicketNotificationsQuery r, CancellationToken ct)
    {
        var q = db.TicketNotifications.AsNoTracking().Where(n => n.UserId == r.UserId);
        var pending = await q.CountAsync(n => n.SeenAt == null || n.OpenedAt == null, ct);
        if (r.OnlyPending) q = q.Where(n => n.SeenAt == null || n.OpenedAt == null);
        var total = await q.CountAsync(ct);
        var size = Math.Clamp(r.PageSize, 1, 100); var page = Math.Max(1, r.Page);
        var items = await q.OrderByDescending(n => n.CreatedAt).Skip((page - 1) * size).Take(size)
            .Select(n => new TicketNotificationDto(n.Id, n.TicketId, n.TrackingNo, n.Kind, n.Message, n.CreatedAt, n.SeenAt, n.OpenedAt)).ToListAsync(ct);
        return Result.Success(new TicketNotificationPageDto(items, total, pending));
    }
}
