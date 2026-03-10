using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Commands.ConvertQuoteToOrder;

public record ConvertQuoteToOrderCommand(
    Guid QuoteId,
    string ShippingRecipientName,
    string ShippingRecipientPhone,
    Guid ShippingCountryId,
    Guid ShippingCityId,
    Guid ShippingDistrictId,
    string ShippingAddressLine,
    string? ShippingPostalCode,
    Guid PaymentMethodId,
    Guid ConvertedBy) : IRequest<Result<Guid>>;
