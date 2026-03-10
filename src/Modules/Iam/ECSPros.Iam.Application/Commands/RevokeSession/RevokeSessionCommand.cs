using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Commands.RevokeSession;

public record RevokeSessionCommand(Guid SessionId) : IRequest<Result<bool>>;

public class RevokeSessionCommandHandler : IRequestHandler<RevokeSessionCommand, Result<bool>>
{
    private readonly IIamDbContext _db;

    public RevokeSessionCommandHandler(IIamDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(RevokeSessionCommand request, CancellationToken ct)
    {
        var session = await _db.UserSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && !s.IsDeleted, ct);

        if (session is null)
            return Result.Failure<bool>("Oturum bulunamadı.");

        session.IsActive = false;
        session.IsDeleted = true;

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
