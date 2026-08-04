using ECSPros.Core.Application.Services;
using ECSPros.Core.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Commands.UpsertNotificationTemplate;

/// <summary>
/// O1 (2026-08-04): bildirim şablonu ekle/güncelle — (tip kodu, kanal, dil) başına tek
/// şablon. Tip yoksa oluşturulur (siparis_onay ilk kayıtta açılır). Panel "Bildirim
/// Şablonları" ekranı bu komutla yazar.
/// </summary>
public record UpsertNotificationTemplateCommand(
    string TypeCode,
    string Channel,          // sms | email
    string LanguageCode,     // tr
    string? Subject,
    string Body,
    bool IsActive = true) : IRequest<Result<Guid>>;

public class UpsertNotificationTemplateCommandHandler(ICoreDbContext db)
    : IRequestHandler<UpsertNotificationTemplateCommand, Result<Guid>>
{
    private static readonly string[] Kanallar = ["sms", "email"];

    public async Task<Result<Guid>> Handle(UpsertNotificationTemplateCommand request, CancellationToken ct)
    {
        if (!Kanallar.Contains(request.Channel))
            return Result.Failure<Guid>("Kanal sms ya da email olmalı.");
        if (string.IsNullOrWhiteSpace(request.Body))
            return Result.Failure<Guid>("Şablon gövdesi boş olamaz.");

        var tip = await db.NotificationTypes.FirstOrDefaultAsync(t => t.Code == request.TypeCode, ct);
        if (tip is null)
        {
            tip = new NotificationType
            {
                Code = request.TypeCode,
                NameI18n = new() { ["tr"] = request.TypeCode == "siparis_onay" ? "Sipariş Onayı" : request.TypeCode },
                DefaultChannels = ["sms", "email"]
            };
            db.NotificationTypes.Add(tip);
        }

        var sablon = await db.NotificationTemplates.FirstOrDefaultAsync(t =>
            t.NotificationTypeId == tip.Id && t.Channel == request.Channel
            && t.LanguageCode == request.LanguageCode, ct);
        if (sablon is null)
        {
            sablon = new NotificationTemplate
            {
                NotificationTypeId = tip.Id,
                LanguageCode = request.LanguageCode,
                Channel = request.Channel
            };
            db.NotificationTemplates.Add(sablon);
        }

        sablon.Subject = request.Channel == "email" ? request.Subject : null;
        sablon.Body = request.Body;
        sablon.IsActive = request.IsActive;
        sablon.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result.Success(sablon.Id);
    }
}
