using System.Security.Cryptography;
using System.Text;
using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.SendLoginOtp;

/// <summary>
/// D4: SMS ile giriş — 1. adım. Kayıtlı üyenin telefonuna 6 haneli tek kullanımlık
/// kod gönderir. Kod 120 sn geçerli (tasarımın 02:00 sayacıyla aynı); aynı numaraya
/// 60 sn içinde yeniden gönderim ve saatte 5'ten fazla kod engellenir. Yeni kod
/// üretildiğinde öncekiler geçersiz kılınır.
/// </summary>
public record SendLoginOtpCommand(string Phone) : IRequest<Result<SendLoginOtpResponse>>;

public record SendLoginOtpResponse(int ExpiresInSeconds);

public static class OtpHelper
{
    public const string LoginPurpose = "login";
    public const int GecerlilikSaniye = 120;
    public const int YenidenGonderimSaniye = 60;
    public const int SaatlikGonderimSiniri = 5;
    public const int DenemeSiniri = 5;

    /// <summary>Telefonu yalnız rakama indirger ve TR ülke koduyla normalize eder
    /// (5551112233 / 05551112233 / 905551112233 → 905551112233).</summary>
    public static string? Normalize(string? phone)
    {
        var rakamlar = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
        rakamlar = rakamlar.TrimStart('0');
        if (rakamlar.Length == 10) rakamlar = "90" + rakamlar;
        return rakamlar.Length == 12 && rakamlar.StartsWith("90") ? rakamlar : null;
    }

    public static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code))).ToLowerInvariant();
}

public class SendLoginOtpCommandHandler(ICrmDbContext db, ISmsSender smsSender)
    : IRequestHandler<SendLoginOtpCommand, Result<SendLoginOtpResponse>>
{
    public async Task<Result<SendLoginOtpResponse>> Handle(SendLoginOtpCommand request, CancellationToken ct)
    {
        var telefon = OtpHelper.Normalize(request.Phone);
        if (telefon is null)
            return Result.Failure<SendLoginOtpResponse>("Geçerli bir telefon numarası girin.");

        // Üye kaydı telefonu ülke kodlu tutar; eski aktarımlarda biçim farkı olabilir —
        // son 10 hane üzerinden eşleştirilir.
        var son10 = telefon[^10..];
        var uyeVar = await db.Members.AnyAsync(
            m => m.IsActive && m.IsRegistered && m.Phone != null && m.Phone.EndsWith(son10), ct);
        if (!uyeVar)
            return Result.Failure<SendLoginOtpResponse>("Bu telefon numarasıyla kayıtlı üye bulunamadı.");

        var simdi = DateTime.UtcNow;
        var sonKodlar = await db.OtpCodes
            .Where(o => o.Phone == telefon && o.Purpose == OtpHelper.LoginPurpose
                        && o.CreatedAt > simdi.AddHours(-1))
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
            Purpose = OtpHelper.LoginPurpose,
            ExpiresAt = simdi.AddSeconds(OtpHelper.GecerlilikSaniye)
        });
        await db.SaveChangesAsync(ct);

        await smsSender.SendAsync(telefon,
            $"Giriş doğrulama kodunuz: {kod}. Kod 2 dakika geçerlidir.", ct);

        return Result.Success(new SendLoginOtpResponse(OtpHelper.GecerlilikSaniye));
    }
}
