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
        var query = _context.Users.Where(u => !u.IsDeleted);

        if (request.ActiveOnly)
            query = query.Where(u => u.IsActive);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.ToLower();
            query = query.Where(u =>
                u.Username.ToLower().Contains(s) ||
                u.Email.ToLower().Contains(s) ||
                u.FirstName.ToLower().Contains(s) ||
                u.LastName.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var users = await query
            .OrderBy(u => u.Username)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new
            {
                u.Id, u.Username, u.Email, u.FirstName, u.LastName,
                u.Department, u.JobTitle, u.IsActive, u.LastLoginAt,
                Roles = u.UserRoles
                    .Where(ur => !ur.IsDeleted)
                    .Select(ur => ur.Role.Code)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var items = users.Select(u => new UserListDto(
            u.Id, u.Username, u.Email, u.FirstName, u.LastName,
            u.Department, u.JobTitle, u.IsActive, u.LastLoginAt, u.Roles
        )).ToList();

        return Result.Success(new PagedUserResult(items, totalCount, request.Page, request.PageSize));
    }
}
