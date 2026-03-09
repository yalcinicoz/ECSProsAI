using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Iam.Application.Commands.CreateUser;

public record CreateUserCommand(
    string Username,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Department,
    string? JobTitle,
    string? Phone,
    bool MustChangePassword = true) : IRequest<Result<Guid>>;
