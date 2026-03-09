using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Iam.Application.Commands.Login;

public record LoginCommand(string Username, string Password) : IRequest<Result<LoginResponse>>;

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    Guid UserId,
    string FullName,
    string Email);
