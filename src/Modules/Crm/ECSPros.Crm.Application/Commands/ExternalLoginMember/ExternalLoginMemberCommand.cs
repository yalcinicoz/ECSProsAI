using ECSPros.Crm.Application.Commands.LoginMember;
using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.ExternalLoginMember;

/// <summary>
/// OAuth dış kimlikle giriş (Google/Facebook). Akış: mevcut dış kimlik → üye;
/// yoksa aynı e-posta ile üye bul → bağla; yoksa yeni üye oluştur + bağla.
/// Ardından normal login ile aynı session + token çiftini üretir.
/// </summary>
public record ExternalLoginMemberCommand(
    string Provider,
    string ProviderUserId,
    string Email,
    string FirstName,
    string LastName,
    bool EmailVerified = false,
    string? IpAddress = null,
    string? UserAgent = null) : IRequest<Result<MemberLoginResponse>>;

public class ExternalLoginMemberCommandHandler(
    ICrmDbContext db, IMemberTokenService tokenService)
    : IRequestHandler<ExternalLoginMemberCommand, Result<MemberLoginResponse>>
{
    public async Task<Result<MemberLoginResponse>> Handle(
        ExternalLoginMemberCommand request, CancellationToken ct)
    {
        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            return Result.Failure<MemberLoginResponse>(
                "Giriş sağlayıcı e-posta adresi paylaşmadı; giriş tamamlanamadı.");

        var provider = request.Provider.Trim().ToLowerInvariant();
        var providerUserId = request.ProviderUserId.Trim();
        if (string.IsNullOrWhiteSpace(providerUserId))
            return Result.Failure<MemberLoginResponse>(
                "Giriş sağlayıcı kullanıcı kimliği döndürmedi.");

        Member member;

        var existing = await db.MemberExternalLogins
            .Include(x => x.Member)
            .FirstOrDefaultAsync(
                x => x.Provider == provider && x.ProviderUserId == providerUserId, ct);

        if (existing is not null && existing.Member is not null)
        {
            member = existing.Member;
            if (!member.IsActive)
                return Result.Failure<MemberLoginResponse>(
                    "Bu hesap devre dışı. Lütfen müşteri hizmetleriyle iletişime geçin.");
        }
        else
        {
            // 2) Aynı e-posta zaten üye → dış kimliği bağla (varsayılan: aktif kayıt).
            member = await db.Members
                .FirstOrDefaultAsync(m => m.Email == email && m.IsActive, ct);

            if (member is null)
            {
                // 3) Yeni üye oluştur.
                var defaultGroup = await db.MemberGroups
                    .FirstOrDefaultAsync(g => g.IsDefault && !g.IsDeleted, ct);
                if (defaultGroup is null)
                    return Result.Failure<MemberLoginResponse>(
                        "Varsayılan üye grubu bulunamadı.");

                member = new Member
                {
                    MemberGroupId = defaultGroup.Id,
                    Email = email,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    IsRegistered = true,
                    IsActive = true,
                    IsEmailVerified = request.EmailVerified
                };
                db.Members.Add(member);
            }
            else if (request.EmailVerified)
            {
                // Sağlayıcı e-postayı doğruladıysa mevcut üyeyi de doğrulanmış işaretle.
                member.IsEmailVerified = true;
            }

            db.MemberExternalLogins.Add(new MemberExternalLogin
            {
                MemberId = member.Id,
                Provider = provider,
                ProviderUserId = providerUserId,
                Email = email
            });
        }

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
