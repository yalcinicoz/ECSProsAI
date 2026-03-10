using System.Security.Cryptography;
using System.Text;
using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.LoginMember;

public record LoginMemberCommand(string Email, string Password) : IRequest<Result<MemberLoginResponse>>;

public record MemberLoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    Guid MemberId,
    string FullName,
    string Email);

public class LoginMemberCommandHandler(ICrmDbContext db, IMemberTokenService tokenService)
    : IRequestHandler<LoginMemberCommand, Result<MemberLoginResponse>>
{
    public async Task<Result<MemberLoginResponse>> Handle(LoginMemberCommand request, CancellationToken ct)
    {
        var member = await db.Members
            .FirstOrDefaultAsync(m => m.Email == request.Email.ToLowerInvariant() && m.IsActive, ct);

        if (member is null)
            return Result.Failure<MemberLoginResponse>("E-posta veya şifre hatalı.");

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Password))).ToLowerInvariant();
        if (member.PasswordHash != hash)
            return Result.Failure<MemberLoginResponse>("E-posta veya şifre hatalı.");

        var rawRefresh = tokenService.GenerateRefreshToken();
        var refreshHash = tokenService.HashRefreshToken(rawRefresh);
        var expiresAt = DateTime.UtcNow.AddDays(30);

        db.MemberSessions.Add(new MemberSession
        {
            MemberId = member.Id,
            RefreshTokenHash = refreshHash,
            ExpiresAt = expiresAt,
            IsActive = true
        });

        member.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var accessToken = tokenService.GenerateAccessToken(member);
        return Result.Success(new MemberLoginResponse(
            accessToken, rawRefresh, expiresAt,
            member.Id, $"{member.FirstName} {member.LastName}", member.Email ?? string.Empty));
    }
}
