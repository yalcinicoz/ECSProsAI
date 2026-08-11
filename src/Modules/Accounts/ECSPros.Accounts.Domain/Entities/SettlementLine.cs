using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Accounts.Domain.Entities;

/// <summary>
/// P3a (2026-08-11): hakediş satırı — teslim edilen sipariş KALEMİ başına bir kayıt
/// (OrderItemId unique; iade ters satırı hariç). Uygulanan oran ve KATMAN İZİ satırda
/// saklanır (K1 "anlaşılır olsun": satıcı 'bu satışta oran neden %X' sorusunu buradan görür).
/// Bakiye etkisi YALNIZ uygunlaşınca (EligibleAt geçince) 'hakedis' defterine
/// PostAccountTransaction ile yazılır — cari çatı altın kuralı.
/// Durumlar: pending → available (defter kaydı atıldı) → paid; iade: reversed (+ ters satır).
/// </summary>
public class SettlementLine : BaseEntity
{
    public Guid SupplierAccountId { get; set; }     // CurrentAccount (supplier)
    public Guid OrderId { get; set; }
    public Guid OrderItemId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }

    /// <summary>Kalemin gerçek ödenen tutarı (OrderItem.Total — kampanya dağıtımı düşülmüş).</summary>
    public decimal GrossAmount { get; set; }
    public decimal CommissionRate { get; set; }
    /// <summary>Uygulanan katman: product | campaign | contract_group | group_default
    /// (+ ciro ayarı uygulandıysa "+turnover" eki, örn "contract_group+turnover").</summary>
    public string CommissionLayer { get; set; } = string.Empty;
    public decimal CommissionAmount { get; set; }
    /// <summary>Kampanya indirim yükünün satıcı payı (alt-karar: paylaşım kampanya tanımında).</summary>
    public decimal CampaignDiscountShareAmount { get; set; }
    public decimal NetAmount { get; set; }

    public Guid? CampaignId { get; set; }
    public string Status { get; set; } = "pending"; // pending | available | paid | reversed
    public DateTime DeliveredAt { get; set; }
    public DateTime EligibleAt { get; set; }
    public DateTime? AvailableAt { get; set; }
    public DateTime? PaidAt { get; set; }
    /// <summary>Uygunlaşınca atılan defter hareketinin kimliği (izlenebilirlik).</summary>
    public Guid? LedgerTransactionId { get; set; }
    /// <summary>İade ters satırı — hangi satırın tersi olduğunu gösterir.</summary>
    public Guid? ReversalOfId { get; set; }
    public string? Description { get; set; }
}
