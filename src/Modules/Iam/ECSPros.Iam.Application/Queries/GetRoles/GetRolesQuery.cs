using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Iam.Application.Queries.GetRoles;

public record GetRolesQuery : IRequest<Result<List<RoleDto>>>;

public record RoleDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    bool IsSystem,
    bool IsActive);
