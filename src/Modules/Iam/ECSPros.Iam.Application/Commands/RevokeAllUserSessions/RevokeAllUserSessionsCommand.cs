using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Commands.RevokeAllUserSessions;

public record RevokeAllUserSessionsCommand(Guid UserId) : IRequest<Result<int>>;

public class RevokeAllUserSessionsCommandHandler : IRequestHandler<RevokeAllUserSessionsCommand, Result<int>>
{
    private readonly IIamDbContext _db;

    public RevokeAllUserSessionsCommandHandler(IIamDbContext db) => _db = db;

    public async Task<Result<int>> Handle(RevokeAllUserSessionsCommand request, CancellationToken ct)
    {
        var sessions = await _db.UserSessions
            .Where(s => s.UserId == request.UserId && s.IsActive && !s.IsDeleted)
            .ToListAsync(ct);

        foreach (var session in sessions)
        {
            session.IsActive = false;
            session.IsDeleted = true;
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success(sessions.Count);
    }
}
