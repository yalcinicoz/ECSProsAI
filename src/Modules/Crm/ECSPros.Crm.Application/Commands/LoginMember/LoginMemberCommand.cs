using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.LoginMember;

public record LoginMemberCommand(
    string Email, string Password,
    string? IpAddress = null, string? UserAgent = null) // E2: Aktif Cihazlar/Giriş Geçmişi
    : IRequest<Result<MemberLoginResponse>>;

public record MemberLoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    Guid MemberId,
    string FullName,
    string Email);

public class LoginMemberCommandHandler(
    ICrmDbContext db, IMemberTokenService tokenService, IMemberPasswordHasher passwordHasher)
    : IRequestHandler<LoginMemberCommand, Result<MemberLoginResponse>>
{
    public async Task<Result<MemberLoginResponse>> Handle(LoginMemberCommand request, CancellationToken ct)
    {
        var member = await db.Members
            .FirstOrDefaultAsync(m => m.Email == request.Email.ToLowerInvariant() && m.IsActive, ct);

        if (member is null || string.IsNullOrEmpty(member.PasswordHash))
            return Result.Failure<MemberLoginResponse>("E-posta veya şifre hatalı.");

        if (!passwordHasher.Verify(request.Password, member.PasswordHash))
            return Result.Failure<MemberLoginResponse>("E-posta veya şifre hatalı.");

        // D5: eski SHA256 hash başarılı girişte BCrypt'e yükseltilir (aşağıdaki
        // SaveChanges ile kalıcılaşır) — toplu migration gerekmez.
        if (passwordHasher.NeedsRehash(member.PasswordHash))
            member.PasswordHash = passwordHasher.Hash(request.Password);

        var rawRefresh = tokenService.GenerateRefreshToken();
        var refreshHash = tokenService.HashRefreshToken(rawRefresh);
        var expiresAt = DateTime.UtcNow.AddDays(30);

        db.MemberSessions.Add(new MemberSession
        {
            MemberId = member.Id,
            RefreshTokenHash = refreshHash,
            ExpiresAt = expiresAt,
            IsActive = true,
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent
        });

        member.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var accessToken = tokenService.GenerateAccessToken(member);
        return Result.Success(new MemberLoginResponse(
            accessToken, rawRefresh, expiresAt,
            member.Id, $"{member.FirstName} {member.LastName}", member.Email ?? string.Empty));
    }
}
