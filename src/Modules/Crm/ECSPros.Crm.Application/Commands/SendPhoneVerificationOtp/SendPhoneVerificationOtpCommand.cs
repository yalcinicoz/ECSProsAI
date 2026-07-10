using System.Security.Cryptography;
using ECSPros.Crm.Application.Commands.SendLoginOtp;
using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.SendPhoneVerificationOtp;

/// <summary>
/// E8: telefon doğrulama SMS'i — iade talebi gibi doğrulanmış telefon gerektiren
/// akışların 1. adımı. D4 giriş OTP'siyle aynı tablo/sınırlar, farkı: kod üyenin
/// KAYITLI telefonuna gider (istemci numara gönderemez) ve doğrulama oturum açmaz,
/// yalnız IsPhoneVerified işaretler. Purpose ayrımı giriş kodlarıyla karışmayı önler.
/// </summary>
public record SendPhoneVerificationOtpCommand(Guid MemberId) : IRequest<Result<SendLoginOtpResponse>>;

public class SendPhoneVerificationOtpCommandHandler(ICrmDbContext db, ISmsSender smsSender)
    : IRequestHandler<SendPhoneVerificationOtpCommand, Result<SendLoginOtpResponse>>
{
    public const string Purpose = "phone_verify";

    public async Task<Result<SendLoginOtpResponse>> Handle(SendPhoneVerificationOtpCommand request, CancellationToken ct)
    {
        var uye = await db.Members.FirstOrDefaultAsync(m => m.Id == request.MemberId && m.IsActive, ct);
        if (uye is null)
            return Result.Failure<SendLoginOtpResponse>("Üye bulunamadı.");

        var telefon = OtpHelper.Normalize(uye.Phone);
        if (telefon is null)
            return Result.Failure<SendLoginOtpResponse>("Üyelik bilgilerinizde geçerli bir telefon numarası yok. Lütfen önce telefon numaranızı güncelleyin.");

        var simdi = DateTime.UtcNow;
        var sonKodlar = await db.OtpCodes
            .Where(o => o.Phone == telefon && o.Purpose == Purpose && o.CreatedAt > simdi.AddHours(-1))
            .ToListAsync(ct);

        if (sonKodlar.Count >= OtpHelper.SaatlikGonderimSiniri)
            return Result.Failure<SendLoginOtpResponse>("Çok fazla kod istendi. Lütfen daha sonra tekrar deneyin.");

        if (sonKodlar.Any(o => o.ConsumedAt == null
                               && o.CreatedAt > simdi.AddSeconds(-OtpHelper.YenidenGonderimSaniye)))
            return Result.Failure<SendLoginOtpResponse>("Yeni kod istemek için lütfen biraz bekleyin.");

        foreach (var eski in sonKodlar.Where(o => o.ConsumedAt == null))
            eski.ConsumedAt = simdi;

        var kod = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        db.OtpCodes.Add(new OtpCode
        {
            Phone = telefon,
            CodeHash = OtpHelper.Hash(kod),
            Purpose = Purpose,
            ExpiresAt = simdi.AddSeconds(OtpHelper.GecerlilikSaniye)
        });
        await db.SaveChangesAsync(ct);

        await smsSender.SendAsync(telefon,
            $"Telefon doğrulama kodunuz: {kod}. Kod 2 dakika geçerlidir.", ct);

        return Result.Success(new SendLoginOtpResponse(OtpHelper.GecerlilikSaniye));
    }
}
