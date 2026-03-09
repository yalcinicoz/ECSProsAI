using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Iam.Application.Commands.ChangePassword;

public record ChangePasswordCommand(
    Guid UserId,
    string? CurrentPassword,
    string NewPassword,
    bool IsAdminReset = false) : IRequest<Result>;
