using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Commands.CreateReturn;

public record ReturnItemRequest(
    Guid OrderItemId,
    Guid VariantId,
    int Quantity,
    Guid ReturnReasonId,
    string? CustomerNotes);

public record CreateReturnCommand(
    Guid OrderId,
    Guid MemberId,
    string ReturnType,
    string? CustomerNotes,
    string RefundMethod,
    List<ReturnItemRequest> Items) : IRequest<Result<Guid>>;
