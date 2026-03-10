using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Queries.GetUserDetail;

public record GetUserDetailQuery(Guid Id) : IRequest<Result<UserDetailDto>>;

public record UserDetailDto(
    Guid Id,
    string Username,
    string Email,
    string FirstName,
    string LastName,
    string? Department,
    string? JobTitle,
    string? Phone,
    bool IsActive,
    bool MustChangePassword,
    List<string> Roles,
    List<string> Permissions,
    DateTime CreatedAt,
    DateTime? LastLoginAt
);

public class GetUserDetailQueryHandler : IRequestHandler<GetUserDetailQuery, Result<UserDetailDto>>
{
    private readonly IIamDbContext _db;

    public GetUserDetailQueryHandler(IIamDbContext db) => _db = db;

    public async Task<Result<UserDetailDto>> Handle(GetUserDetailQuery request, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.UserPermissions).ThenInclude(up => up.Permission)
            .FirstOrDefaultAsync(u => u.Id == request.Id, ct);

        if (user is null)
            return Result.Failure<UserDetailDto>("Kullanıcı bulunamadı.");

        var lastSession = await _db.UserSessions
            .Where(s => s.UserId == request.Id && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => (DateTime?)s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var roles = user.UserRoles.Where(ur => !ur.IsDeleted).Select(ur => ur.Role.Code).ToList();
        var permissions = user.UserPermissions.Where(up => !up.IsDeleted).Select(up => up.Permission.Code).ToList();

        var dto = new UserDetailDto(
            user.Id, user.Username, user.Email, user.FirstName, user.LastName,
            user.Department, user.JobTitle, user.Phone, user.IsActive,
            user.MustChangePassword, roles, permissions, user.CreatedAt, lastSession);

        return Result.Success(dto);
    }
}
