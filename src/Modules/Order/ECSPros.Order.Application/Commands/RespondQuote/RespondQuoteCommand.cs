using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Commands.RespondQuote;

/// <summary>Müşteri teklifi kabul veya reddeder.</summary>
public record RespondQuoteCommand(
    Guid QuoteId,
    bool Accepted,
    Guid RespondedBy) : IRequest<Result<bool>>;
