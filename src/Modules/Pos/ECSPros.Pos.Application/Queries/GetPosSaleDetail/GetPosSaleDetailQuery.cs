using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Pos.Application.Queries.GetPosSaleDetail;

public record GetPosSaleDetailQuery(Guid SaleId) : IRequest<Result<PosSaleDetailDto>>;

public record PosSaleDetailDto(
    Guid Id,
    string SaleNumber,
    Guid SessionId,
    Guid RegisterId,
    Guid? MemberId,
    string Status,
    decimal Subtotal,
    decimal TotalDiscount,
    decimal TotalTax,
    decimal GrandTotal,
    string? Notes,
    DateTime CreatedAt,
    List<PosSaleItemDto> Items,
    List<PosSalePaymentDto> Payments);

public record PosSaleItemDto(
    Guid Id,
    Guid VariantId,
    string? Barcode,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal TaxRate,
    decimal TaxAmount,
    decimal LineTotal);

public record PosSalePaymentDto(
    Guid Id,
    string PaymentMethod,
    decimal Amount,
    decimal? TenderedAmount,
    decimal? ChangeAmount);
