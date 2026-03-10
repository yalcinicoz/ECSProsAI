using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Commands.CancelInvoice;

public record CancelInvoiceCommand(Guid InvoiceId, Guid CancelledBy) : IRequest<Result<bool>>;
