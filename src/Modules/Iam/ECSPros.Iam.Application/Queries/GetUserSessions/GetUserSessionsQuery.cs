using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Queries.GetUserSessions;

public record GetUserSessionsQuery(Guid UserId, bool ActiveOnly = true) : IRequest<Result<List<UserSessionDto>>>;

public record UserSessionDto(
    Guid Id,
    Guid UserId,
    string IpAddress,
    string? UserAgent,
    DateTime ExpiresAt,
    DateTime LastActivityAt,
    bool IsActive,
    DateTime CreatedAt
);

public class GetUserSessionsQueryHandler : IRequestHandler<GetUserSessionsQuery, Result<List<UserSessionDto>>>
{
    private readonly IIamDbContext _db;

    public GetUserSessionsQueryHandler(IIamDbContext db) => _db = db;

    public async Task<Result<List<UserSessionDto>>> Handle(GetUserSessionsQuery request, CancellationToken ct)
    {
        var query = _db.UserSessions.Where(s => s.UserId == request.UserId && !s.IsDeleted);
        if (request.ActiveOnly)
            query = query.Where(s => s.IsActive && s.ExpiresAt > DateTime.UtcNow);

        var list = await query
            .OrderByDescending(s => s.LastActivityAt)
            .Select(s => new UserSessionDto(
                s.Id, s.UserId, s.IpAddress, s.UserAgent,
                s.ExpiresAt, s.LastActivityAt, s.IsActive, s.CreatedAt))
            .ToListAsync(ct);

        return Result.Success<List<UserSessionDto>>(list);
    }
}
