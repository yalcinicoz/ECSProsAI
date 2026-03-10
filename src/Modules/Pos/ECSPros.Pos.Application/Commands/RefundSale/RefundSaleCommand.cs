using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Pos.Application.Commands.RefundSale;

public record RefundSaleCommand(
    Guid SaleId,
    string? Reason,
    Guid RefundedBy) : IRequest<Result<bool>>;
