using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Commands.CreateQuote;

public record QuoteItemRequest(
    Guid VariantId,
    string Sku,
    string ProductName,
    string VariantInfo,
    int Quantity,
    string UnitType,
    decimal UnitPrice,
    decimal DiscountRate,
    decimal TaxRate,
    string? Notes);

public record CreateQuoteCommand(
    Guid FirmPlatformId,
    Guid MemberId,
    string CurrencyCode,
    DateTime ValidUntil,
    string? NotesToCustomer,
    string? InternalNotes,
    List<QuoteItemRequest> Items,
    Guid CreatedBy) : IRequest<Result<Guid>>;
