using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Queries.GetUsers;

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, Result<PagedUserResult>>
{
    private readonly IIamDbContext _context;

    public GetUsersQueryHandler(IIamDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedUserResult>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        // DataGrid (2026-09-08): adlandırılmış filtre + global arama + beyaz listeli grid filtreleri TEK yerden (UserGrid).
        var query = UserGrid.ApplyAll(_context.Users.AsQueryable(), new UserListFilters(request.Search, request.ActiveOnly), request.Grid);

        var totalCount = await query.CountAsync(cancellationToken);

        var users = await UserGrid.Schema.ApplySort(query, request.Grid)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new
            {
                u.Id, u.Username, u.Email, u.FirstName, u.LastName,
                u.Department, u.JobTitle, u.Phone, u.IsActive, u.LastLoginAt, u.IsSuperAdmin,
                Roles = u.UserRoles
                    .Where(ur => !ur.IsDeleted)
                    .Select(ur => ur.Role.Code)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var items = users.Select(u => new UserListDto(
            u.Id, u.Username, u.Email, u.FirstName, u.LastName,
            u.Department, u.JobTitle, u.Phone, u.IsActive, u.LastLoginAt, u.Roles, u.IsSuperAdmin
        )).ToList();

        return Result.Success(new PagedUserResult(items, totalCount, request.Page, request.PageSize));
    }
}
