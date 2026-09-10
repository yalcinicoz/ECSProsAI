using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Commands.CompleteRefund;

public record CompleteRefundCommand(
    Guid ReturnId,
    string RefundMethod,
    decimal Amount,
    Guid ProcessedBy,
    Dictionary<string, object>? Details = null,
    string? Iban = null,              // 15.4g: havale ile iadede zorunlu; Return'e ve ödeme kaydına yazılır
    string? AccountHolder = null) : IRequest<Result<bool>>;
