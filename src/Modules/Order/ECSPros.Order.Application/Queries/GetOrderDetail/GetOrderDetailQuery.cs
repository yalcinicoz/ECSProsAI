using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Queries.GetOrderDetail;

public record GetOrderDetailQuery(Guid OrderId) : IRequest<Result<OrderDetailDto>>;

public partial record OrderDetailDto(
    Guid Id,
    string OrderNumber,
    Guid? MemberId,
    string Status,
    string PaymentStatus,
    string OrderType,
    string CurrencyCode,
    decimal Subtotal,
    decimal TotalDiscount,
    decimal TotalTax,
    decimal GrandTotal,
    string ShippingRecipientName,
    string ShippingRecipientPhone,
    string ShippingAddressLine,
    Guid? PickingPlanId,
    string? InternalNotes,
    DateTime CreatedAt,
    DateTime? ConfirmedAt,
    List<OrderDetailItemDto> Items,
    List<OrderDetailPaymentDto> Payments,
    // P1b additive alanlar — admin sipariş detayı (adresler, sözleşme kabulleri, platform)
    Guid FirmPlatformId = default,
    decimal TotalExpense = 0,
    string? ShippingPostalCode = null,
    string? ShippingDeliveryNotes = null,
    Guid? ShippingCityId = null,
    Guid? ShippingDistrictId = null,
    Guid? ShippingNeighborhoodId = null,
    bool BillingSameAsShipping = true,
    string? BillingRecipientName = null,
    string? BillingCompanyName = null,
    string? BillingTaxOffice = null,
    string? BillingTaxNumber = null,
    string? BillingAddressLine = null,
    Guid? BillingCityId = null,
    Guid? BillingDistrictId = null,
    Dictionary<string, object>? CustomerNotes = null,
    // 2026-07-22: müşterinin teslimat adımındaki kargo tercihi (mahalle bazlı seçenekler)
    Guid? RequestedCargoIntegrationId = null,
    string? RequestedCargoName = null,
    string? PaymentMethod = null,   // 2026-08-04: kart | kapida-nakit | kapida-kart | null
    decimal ShippingFee = 0);       // 2026-09-09: kargo bedeli (TotalExpense = kapıda ödeme bedeli, ayrı)

// ── M4 (2026-09-09, mobil): sipariş detayının vitrin etiketleri + akış şeridi + aksiyon bayrakları.
// Türetilmiş alanlar (handler dokunulmadı). Panel bu alanları okumaz, kendi haritasını kullanır.
public partial record OrderDetailDto
{
    public string StatusLabel => DurumEtiketleri.Etiket(DurumEtiketleri.Vitrin.SiparisDurumu, Status);
    public string StatusColor => DurumEtiketleri.Renk(DurumEtiketleri.Vitrin.SiparisDurumu, Status);
    public string StatusVariant => DurumEtiketleri.Varyant(DurumEtiketleri.Vitrin.SiparisDurumu, Status);
    public string PaymentStatusLabel => DurumEtiketleri.Etiket(DurumEtiketleri.Vitrin.OdemeDurumu, PaymentStatus);
    public string PaymentMethodLabel => DurumEtiketleri.Etiket(DurumEtiketleri.Vitrin.OdemeYontemi, PaymentMethod);

    /// <summary>4 adımlı akış şeridi; İPTAL edilen siparişte BOŞ (istemci şeridi çizmez).</summary>
    public List<AkisAdimi> Timeline => DurumEtiketleri.SiparisAkisi(Status);

    /// <summary>Müşteri aksiyonları — komutların dayattığı kuralın aynısı (istemci butonu buna göre gizler).</summary>
    public bool CanCancel => DurumEtiketleri.IptalEdilebilir(Status);
    public bool CanReturn => DurumEtiketleri.IadeEdilebilir(Status);
    public bool CanReview => DurumEtiketleri.YorumYazilabilir(Status);
}

public record OrderDetailItemDto(
    Guid Id,
    Guid VariantId,
    string Sku,
    string ProductName,
    string VariantInfo,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal Total,
    string Status,
    // A1/A2 (2026-09-07, mobil): kalem görseli + ürün kodu + renk/beden değer kimlikleri — Catalog'dan
    // IProductService ile zenginleştirilir; varyant silinmişse null kalır.
    string? ProductCode = null,
    string? ImageUrl = null,
    Guid? ColorValueId = null,
    Guid? SizeValueId = null);

public record OrderDetailPaymentDto(
    Guid Id,
    Guid PaymentMethodId,
    decimal Amount,
    string CurrencyCode,
    string Status);
