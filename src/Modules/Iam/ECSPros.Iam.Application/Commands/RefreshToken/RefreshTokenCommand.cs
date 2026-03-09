using ECSPros.Iam.Application.Commands.Login;
using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Iam.Application.Commands.RefreshToken;

public record RefreshTokenCommand(string RefreshToken) : IRequest<Result<LoginResponse>>;
