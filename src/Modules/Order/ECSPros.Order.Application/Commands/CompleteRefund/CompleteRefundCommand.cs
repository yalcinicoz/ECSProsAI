using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Commands.CompleteRefund;

public record CompleteRefundCommand(
    Guid ReturnId,
    string RefundMethod,
    decimal Amount,
    Guid ProcessedBy,
    Dictionary<string, object>? Details = null) : IRequest<Result<bool>>;
