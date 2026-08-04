using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Queries.GetNotificationTemplates;

/// <summary>O1 (2026-08-04): tip koduna göre bildirim şablonları (panel ekranı) — kayıt
/// yoksa boş döner, panel varsayılan şablon metnini kendisi önerir.</summary>
public record GetNotificationTemplatesQuery(string TypeCode) : IRequest<Result<List<NotificationTemplateDto>>>;

public record NotificationTemplateDto(
    Guid Id, string TypeCode, string Channel, string LanguageCode,
    string? Subject, string Body, bool IsActive, DateTime? UpdatedAt);

public class GetNotificationTemplatesQueryHandler(ICoreDbContext db)
    : IRequestHandler<GetNotificationTemplatesQuery, Result<List<NotificationTemplateDto>>>
{
    public async Task<Result<List<NotificationTemplateDto>>> Handle(
        GetNotificationTemplatesQuery request, CancellationToken ct)
    {
        var liste = await db.NotificationTemplates.AsNoTracking()
            .Where(t => t.NotificationType.Code == request.TypeCode)
            .Select(t => new NotificationTemplateDto(
                t.Id, t.NotificationType.Code, t.Channel, t.LanguageCode,
                t.Subject, t.Body, t.IsActive, t.UpdatedAt))
            .ToListAsync(ct);
        return Result.Success(liste);
    }
}
