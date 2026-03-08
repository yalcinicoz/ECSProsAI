using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Core.Domain.Entities;

public class FirmNotificationSetting : BaseEntity
{
    public Guid FirmId { get; set; }
    public Guid NotificationTypeId { get; set; }
    public bool IsEnabled { get; set; } = true;
    public List<string> Channels { get; set; } = new(); // sms, email, push
    public Guid? SmsProviderId { get; set; }
    public Guid? EmailProviderId { get; set; }
    public Guid? PushProviderId { get; set; }

    public Firm Firm { get; set; } = null!;
    public NotificationType NotificationType { get; set; } = null!;
}
