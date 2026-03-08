using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Core.Domain.Entities;

public class NotificationType : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public List<string> DefaultChannels { get; set; } = new(); // sms, email, push
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    public ICollection<NotificationTemplate> Templates { get; set; } = new List<NotificationTemplate>();
    public ICollection<FirmNotificationSetting> FirmSettings { get; set; } = new List<FirmNotificationSetting>();
}
