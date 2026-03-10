using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Pos.Application.Commands.CompleteSale;

public record SaleItemDto(
    Guid VariantId,
    string? Barcode,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal TaxRate);

public record SalePaymentDto(
    string PaymentMethod,
    decimal Amount,
    decimal? TenderedAmount,
    decimal? ChangeAmount);

public record CompleteSaleCommand(
    Guid SessionId,
    Guid? MemberId,
    List<SaleItemDto> Items,
    List<SalePaymentDto> Payments,
    string? Notes,
    Guid CompletedBy) : IRequest<Result<Guid>>;
