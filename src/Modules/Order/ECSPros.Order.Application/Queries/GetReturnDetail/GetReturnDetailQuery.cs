using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Queries.GetReturnDetail;

public record GetReturnDetailQuery(Guid ReturnId) : IRequest<Result<ReturnDetailDto>>;

public record ReturnDetailDto(
    Guid Id,
    string ReturnNumber,
    Guid OrderId,
    Guid MemberId,
    string ReturnType,
    string? CustomerNotes,
    string Status,
    string? ReturnTrackingNumber,
    DateTime? ReturnCargoSentAt,
    DateTime? ReturnCargoReceivedAt,
    string? InspectionNotes,
    DateTime? InspectionCompletedAt,
    string RefundMethod,
    string RefundStatus,
    decimal RefundAmount,
    DateTime CreatedAt,
    List<ReturnItemDto> Items,
    List<ReturnRefundDto> Refunds);

public record ReturnItemDto(
    Guid Id,
    Guid OrderItemId,
    Guid VariantId,
    int Quantity,
    Guid ReturnReasonId,
    string? CustomerNotes,
    decimal UnitRefundAmount,
    decimal TotalRefundAmount,
    string Status,
    string? InspectionResult,
    string? InspectionNotes);

public record ReturnRefundDto(
    Guid Id,
    string RefundMethod,
    decimal Amount,
    string Status,
    DateTime? ProcessedAt);
