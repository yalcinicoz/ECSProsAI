using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Commands.CreateOrder;

public record CreateOrderCommand(
    Guid FirmPlatformId,
    Guid MemberId,
    string OrderType,
    string PaymentMethod,
    string CurrencyCode,
    // Shipping
    string ShippingRecipientName,
    string ShippingRecipientPhone,
    Guid ShippingCountryId,
    Guid ShippingCityId,
    Guid ShippingDistrictId,
    string ShippingAddressLine,
    string? ShippingPostalCode,
    // Items
    List<OrderItemDto> Items,
    string? CustomerNotes = null) : IRequest<Result<string>>;

public record OrderItemDto(
    Guid VariantId,
    int Quantity,
    decimal UnitPrice,
    string? UnitType = null);
