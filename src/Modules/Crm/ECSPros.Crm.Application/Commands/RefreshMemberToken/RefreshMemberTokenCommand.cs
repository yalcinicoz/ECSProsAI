using ECSPros.Crm.Application.Commands.LoginMember;
using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.RefreshMemberToken;

public record RefreshMemberTokenCommand(string RefreshToken) : IRequest<Result<MemberLoginResponse>>;

public class RefreshMemberTokenCommandHandler(ICrmDbContext db, IMemberTokenService tokenService)
    : IRequestHandler<RefreshMemberTokenCommand, Result<MemberLoginResponse>>
{
    public async Task<Result<MemberLoginResponse>> Handle(RefreshMemberTokenCommand request, CancellationToken ct)
    {
        var hash = tokenService.HashRefreshToken(request.RefreshToken);

        var session = await db.MemberSessions
            .Include(s => s.Member)
            .FirstOrDefaultAsync(s => s.RefreshTokenHash == hash && s.IsActive && s.ExpiresAt > DateTime.UtcNow, ct);

        if (session is null)
            return Result.Failure<MemberLoginResponse>("Geçersiz veya süresi dolmuş refresh token.");

        // Rotate
        session.IsActive = false;
        session.UpdatedAt = DateTime.UtcNow;

        var rawRefresh = tokenService.GenerateRefreshToken();
        var refreshHash = tokenService.HashRefreshToken(rawRefresh);
        var expiresAt = DateTime.UtcNow.AddDays(30);

        db.MemberSessions.Add(new MemberSession
        {
            MemberId = session.MemberId,
            RefreshTokenHash = refreshHash,
            ExpiresAt = expiresAt,
            IsActive = true
        });

        await db.SaveChangesAsync(ct);

        var accessToken = tokenService.GenerateAccessToken(session.Member);
        return Result.Success(new MemberLoginResponse(
            accessToken, rawRefresh, expiresAt,
            session.Member.Id,
            $"{session.Member.FirstName} {session.Member.LastName}",
            session.Member.Email ?? string.Empty));
    }
}
