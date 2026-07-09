using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.RevokeMemberSession;

/// <summary>
/// D1/D6: çıkışta refresh oturumunu iptal eder (session IsActive=false).
/// İdempotent — oturum bulunamazsa da başarı döner (çıkış her koşulda tamamlanır).
/// </summary>
public record RevokeMemberSessionCommand(string RefreshToken) : IRequest<Result<bool>>;

public class RevokeMemberSessionCommandHandler(ICrmDbContext db, IMemberTokenService tokenService)
    : IRequestHandler<RevokeMemberSessionCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(RevokeMemberSessionCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return Result.Success(false);

        var hash = tokenService.HashRefreshToken(request.RefreshToken);
        var session = await db.MemberSessions
            .FirstOrDefaultAsync(s => s.RefreshTokenHash == hash && s.IsActive, ct);
        if (session is null)
            return Result.Success(false);

        session.IsActive = false;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
