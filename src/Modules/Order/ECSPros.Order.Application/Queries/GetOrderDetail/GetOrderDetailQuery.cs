using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Queries.GetOrderDetail;

public record GetOrderDetailQuery(Guid OrderId) : IRequest<Result<OrderDetailDto>>;

public record OrderDetailDto(
    Guid Id,
    string OrderNumber,
    Guid MemberId,
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
    List<OrderDetailPaymentDto> Payments);

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
