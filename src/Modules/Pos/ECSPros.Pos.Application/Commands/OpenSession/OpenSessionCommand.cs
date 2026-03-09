using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Pos.Application.Commands.OpenSession;

public record OpenSessionCommand(
    Guid RegisterId,
    Guid UserId,
    decimal OpeningCash,
    string? Notes) : IRequest<Result<Guid>>;
