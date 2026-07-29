using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ECSPros.Crm.Application.Commands.LoginMember;

public record LoginMemberCommand(
    string Email, string Password,
    string? IpAddress = null, string? UserAgent = null, // E2: Aktif Cihazlar/Giriş Geçmişi
    string? Phone = null) // 2026-07-16: telefon+şifre girişi — doluysa e-posta yerine telefonla aranır
    : IRequest<Result<MemberLoginResponse>>;

public record MemberLoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    Guid MemberId,
    string FullName,
    string Email);

public class LoginMemberCommandHandler(
    ICrmDbContext db, IMemberTokenService tokenService, IMemberPasswordHasher passwordHasher,
    IMemoryCache cache, ILogger<LoginMemberCommandHandler> logger)
    : IRequestHandler<LoginMemberCommand, Result<MemberLoginResponse>>
{
    // Brute-force freni (2026-07-23): hesap bazlı hatalı deneme sayacı — OTP tarafındaki
    // deneme sınırı deseninin şifreli giriş karşılığı. Süreç içi IMemoryCache yeterli
    // (tek host; IP bazlı hacim freni nginx'te). Kilitliyken denemeler sayacı UZATMAZ —
    // hesabın sahibi saldırı altındayken kalıcı kilitlenmesin diye.
    private const int DenemeSiniri = 5;
    private static readonly TimeSpan KilitSuresi = TimeSpan.FromMinutes(15);

    public async Task<Result<MemberLoginResponse>> Handle(LoginMemberCommand request, CancellationToken ct)
    {
        var telefonla = !string.IsNullOrWhiteSpace(request.Phone);
        var kimlik = telefonla ? request.Phone!.Trim() : request.Email.Trim().ToLowerInvariant();
        var kilitAnahtari = $"uye-giris-hata:{kimlik}";
        var hataMesaji = telefonla ? "Telefon veya şifre hatalı." : "E-posta veya şifre hatalı.";

        if (cache.TryGetValue<int>(kilitAnahtari, out var mevcutHata) && mevcutHata >= DenemeSiniri)
            return Result.Failure<MemberLoginResponse>(
                "Çok fazla hatalı deneme yapıldı. Lütfen 15 dakika sonra tekrar deneyin.");

        Result<MemberLoginResponse> HataliDeneme()
        {
            var sayi = (cache.TryGetValue<int>(kilitAnahtari, out var s) ? s : 0) + 1;
            cache.Set(kilitAnahtari, sayi, KilitSuresi);
            if (sayi >= DenemeSiniri)
                logger.LogWarning(
                    "Üye girişi kilitlendi: {Kimlik} — {Sayi} hatalı deneme (IP: {Ip})",
                    kimlik, sayi, request.IpAddress);
            return Result.Failure<MemberLoginResponse>(hataMesaji);
        }

        var member = telefonla
            ? await db.Members.FirstOrDefaultAsync(m => m.Phone == request.Phone && m.IsActive, ct)
            : await db.Members.FirstOrDefaultAsync(m => m.Email == request.Email.ToLowerInvariant() && m.IsActive, ct);

        if (member is null || string.IsNullOrEmpty(member.PasswordHash))
            return HataliDeneme();

        if (!passwordHasher.Verify(request.Password, member.PasswordHash))
            return HataliDeneme();

        cache.Remove(kilitAnahtari);

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
