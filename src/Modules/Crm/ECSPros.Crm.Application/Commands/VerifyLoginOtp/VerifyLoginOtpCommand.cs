using ECSPros.Crm.Application.Commands.LoginMember;
using ECSPros.Crm.Application.Commands.SendLoginOtp;
using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.VerifyLoginOtp;

/// <summary>
/// D4: SMS ile giriş — 2. adım. Kod doğruysa üyeye şifresiz oturum açılır
/// (LoginMember ile aynı session + token akışı) ve telefon doğrulanmış işaretlenir.
/// Kod tek kullanımlıktır; yanlış denemeler sayılır, sınır aşımında kod yanar.
/// </summary>
public record VerifyLoginOtpCommand(string Phone, string Code) : IRequest<Result<MemberLoginResponse>>;

public class VerifyLoginOtpCommandHandler(ICrmDbContext db, IMemberTokenService tokenService)
    : IRequestHandler<VerifyLoginOtpCommand, Result<MemberLoginResponse>>
{
    public async Task<Result<MemberLoginResponse>> Handle(VerifyLoginOtpCommand request, CancellationToken ct)
    {
        var telefon = OtpHelper.Normalize(request.Phone);
        if (telefon is null || string.IsNullOrWhiteSpace(request.Code))
            return Result.Failure<MemberLoginResponse>("Telefon numarası ve kod gereklidir.");

        var simdi = DateTime.UtcNow;
        var otp = await db.OtpCodes
            .Where(o => o.Phone == telefon && o.Purpose == OtpHelper.LoginPurpose && o.ConsumedAt == null)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (otp is null || otp.ExpiresAt < simdi)
            return Result.Failure<MemberLoginResponse>("Kodun süresi doldu. Lütfen yeni kod isteyin.");

        otp.AttemptCount++;
        if (otp.AttemptCount > OtpHelper.DenemeSiniri)
        {
            otp.ConsumedAt = simdi;
            await db.SaveChangesAsync(ct);
            return Result.Failure<MemberLoginResponse>("Çok fazla hatalı deneme. Lütfen yeni kod isteyin.");
        }

        if (otp.CodeHash != OtpHelper.Hash(request.Code.Trim()))
        {
            await db.SaveChangesAsync(ct); // deneme sayısı kalıcı olmalı
            return Result.Failure<MemberLoginResponse>("Kod hatalı. Lütfen kontrol edip tekrar deneyin.");
        }

        otp.ConsumedAt = simdi;

        var son10 = telefon[^10..];
        var member = await db.Members.FirstOrDefaultAsync(
            m => m.IsActive && m.IsRegistered && m.Phone != null && m.Phone.EndsWith(son10), ct);
        if (member is null)
        {
            await db.SaveChangesAsync(ct);
            return Result.Failure<MemberLoginResponse>("Bu telefon numarasıyla kayıtlı üye bulunamadı.");
        }

        var rawRefresh = tokenService.GenerateRefreshToken();
        var expiresAt = simdi.AddDays(30);
        db.MemberSessions.Add(new MemberSession
        {
            MemberId = member.Id,
            RefreshTokenHash = tokenService.HashRefreshToken(rawRefresh),
            ExpiresAt = expiresAt,
            IsActive = true
        });

        member.IsPhoneVerified = true; // kod telefona ulaştı ve doğrulandı
        member.LastLoginAt = simdi;
        await db.SaveChangesAsync(ct);

        var accessToken = tokenService.GenerateAccessToken(member);
        return Result.Success(new MemberLoginResponse(
            accessToken, rawRefresh, expiresAt,
            member.Id, $"{member.FirstName} {member.LastName}", member.Email ?? string.Empty));
    }
}
