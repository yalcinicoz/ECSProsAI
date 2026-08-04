using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Queries.GetOrderDetail;

public record GetOrderDetailQuery(Guid OrderId) : IRequest<Result<OrderDetailDto>>;

public record OrderDetailDto(
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
    string? PaymentMethod = null);   // 2026-08-04: kart | kapida-nakit | kapida-kart | null

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
    string Status);

public record OrderDetailPaymentDto(
    Guid Id,
    Guid PaymentMethodId,
    decimal Amount,
    string CurrencyCode,
    string Status);
