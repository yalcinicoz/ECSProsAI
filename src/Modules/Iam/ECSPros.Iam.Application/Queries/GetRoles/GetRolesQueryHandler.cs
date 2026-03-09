using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Queries.GetRoles;

public class GetRolesQueryHandler : IRequestHandler<GetRolesQuery, Result<List<RoleDto>>>
{
    private readonly IIamDbContext _context;

    public GetRolesQueryHandler(IIamDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<RoleDto>>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
    {
        var items = await _context.Roles
            .Where(r => !r.IsDeleted)
            .OrderBy(r => r.Code)
            .Select(r => new RoleDto(r.Id, r.Code, r.NameI18n, r.IsSystem, r.IsActive))
            .ToListAsync(cancellationToken);

        return Result.Success(items);
    }
}
