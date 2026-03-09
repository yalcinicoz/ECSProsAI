using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Iam.Application.Commands.UpdateUser;

public record UpdateUserCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string Department,
    string? JobTitle,
    string? Phone,
    bool IsActive) : IRequest<Result>;
