using ECSPros.Crm.Application.Commands.SendLoginOtp;
using ECSPros.Crm.Application.Commands.SendPhoneVerificationOtp;
using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.VerifyPhoneVerificationOtp;

/// <summary>
/// E8: telefon doğrulama — 2. adım. Kod doğruysa üyenin telefonu doğrulanmış
/// işaretlenir (oturum açılmaz — üye zaten oturumda). Kod tek kullanımlık;
/// yanlış denemeler sayılır, sınır aşımında kod yanar (D4 kurallarıyla aynı).
/// </summary>
public record VerifyPhoneVerificationOtpCommand(Guid MemberId, string Code) : IRequest<Result>;

public class VerifyPhoneVerificationOtpCommandHandler(ICrmDbContext db)
    : IRequestHandler<VerifyPhoneVerificationOtpCommand, Result>
{
    public async Task<Result> Handle(VerifyPhoneVerificationOtpCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return Result.Failure("Doğrulama kodu gereklidir.");

        var uye = await db.Members.FirstOrDefaultAsync(m => m.Id == request.MemberId && m.IsActive, ct);
        if (uye is null)
            return Result.Failure("Üye bulunamadı.");

        var telefon = OtpHelper.Normalize(uye.Phone);
        if (telefon is null)
            return Result.Failure("Üyelik bilgilerinizde geçerli bir telefon numarası yok.");

        var simdi = DateTime.UtcNow;
        var otp = await db.OtpCodes
            .Where(o => o.Phone == telefon
                        && o.Purpose == SendPhoneVerificationOtpCommandHandler.Purpose
                        && o.ConsumedAt == null)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (otp is null || otp.ExpiresAt < simdi)
            return Result.Failure("Kodun süresi doldu. Lütfen yeni kod isteyin.");

        otp.AttemptCount++;
        if (otp.AttemptCount > OtpHelper.DenemeSiniri)
        {
            otp.ConsumedAt = simdi;
            await db.SaveChangesAsync(ct);
            return Result.Failure("Çok fazla hatalı deneme. Lütfen yeni kod isteyin.");
        }

        if (otp.CodeHash != OtpHelper.Hash(request.Code.Trim()))
        {
            await db.SaveChangesAsync(ct); // deneme sayısı kalıcı olmalı
            return Result.Failure("Kod hatalı. Lütfen kontrol edip tekrar deneyin.");
        }

        otp.ConsumedAt = simdi;
        uye.IsPhoneVerified = true;
        await db.SaveChangesAsync(ct);

        return Result.Success();
    }
}
