using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Pos.Application.Commands.CloseSession;

public record CloseSessionCommand(
    Guid SessionId,
    decimal ClosingCash,
    string? Notes) : IRequest<Result<bool>>;
