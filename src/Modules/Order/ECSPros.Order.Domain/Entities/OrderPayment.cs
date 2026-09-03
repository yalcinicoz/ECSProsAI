using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

public class OrderPayment : BaseEntity
{
    /// <summary>Geçici legacy MySQL importunda oporderpayments.Id.</summary>
    public int? LegacyOrderPaymentId { get; set; }
    public Guid OrderId { get; set; }
    public Guid PaymentMethodId { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, object>? Details { get; set; }

    public Order Order { get; set; } = null!;
}
