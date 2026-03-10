using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Commands.SendQuote;

public record SendQuoteCommand(Guid QuoteId, Guid SentBy) : IRequest<Result<bool>>;
