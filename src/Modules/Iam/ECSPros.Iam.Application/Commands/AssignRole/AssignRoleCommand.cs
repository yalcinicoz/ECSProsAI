using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Iam.Application.Commands.AssignRole;

public record AssignRoleCommand(Guid UserId, Guid RoleId) : IRequest<Result>;
