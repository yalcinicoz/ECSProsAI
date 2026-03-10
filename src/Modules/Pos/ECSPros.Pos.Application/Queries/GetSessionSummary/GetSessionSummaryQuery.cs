using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Pos.Application.Queries.GetSessionSummary;

public record GetSessionSummaryQuery(Guid SessionId) : IRequest<Result<SessionSummaryDto>>;

public record SessionSummaryDto(
    Guid SessionId,
    Guid RegisterId,
    string RegisterName,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    string Status,
    decimal OpeningCash,
    decimal ClosingCash,
    int TotalSaleCount,
    decimal TotalSalesAmount,
    int RefundCount,
    decimal TotalRefundAmount,
    decimal NetAmount,
    List<PaymentMethodSummaryDto> ByPaymentMethod,
    decimal ExpectedCash,
    decimal CashDifference);

public record PaymentMethodSummaryDto(
    string PaymentMethod,
    int SaleCount,
    decimal TotalAmount);
