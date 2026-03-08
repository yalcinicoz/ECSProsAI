using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Core.Domain.Entities;

public class NotificationTemplate : BaseEntity
{
    public Guid NotificationTypeId { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty; // sms, email, push
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public NotificationType NotificationType { get; set; } = null!;
}
