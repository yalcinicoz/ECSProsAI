using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Commands.RevokeSupplierUserSession;

public record RevokeSupplierUserSessionCommand(string RefreshToken) : IRequest<Result>;

public class RevokeSupplierUserSessionCommandHandler(IIamDbContext db, ISupplierUserTokenService tokenService)
    : IRequestHandler<RevokeSupplierUserSessionCommand, Result>
{
    public async Task<Result> Handle(RevokeSupplierUserSessionCommand request, CancellationToken ct)
    {
        var hash = tokenService.HashRefreshToken(request.RefreshToken);
        var session = await db.SupplierUserSessions
            .FirstOrDefaultAsync(s => s.RefreshTokenHash == hash && s.IsActive, ct);
        if (session is not null)
        {
            session.IsActive = false;
            session.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return Result.Success();
    }
}
