using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

public class OrderNotification : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid NotificationTypeId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? ProviderId { get; set; }
    public string? ProviderReference { get; set; }
    public Dictionary<string, object>? ProviderResponse { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public string? FailureReason { get; set; }
    public int RetryCount { get; set; } = 0;
    public DateTime? NextRetryAt { get; set; }

    public Order Order { get; set; } = null!;
}
