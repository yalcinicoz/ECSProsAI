using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.PushDevices;

/// <summary>
/// Mobil push cihaz kaydı (2026-09-05): uygulama token'ı her değişimde ve her
/// giriş/çıkış sonrasında gönderir — kayıt (FirmPlatformId, Token) üzerinden upsert'tir
/// ve gönderilen üye durumu kaydın son halidir (girişli → bağlanır, girişsiz → koparılır).
/// Aynı DeviceIdentifier yeni token'la gelirse eski token kayıtları revoked'a çekilir.
/// </summary>
public record RegisterPushDeviceCommand(
    Guid FirmPlatformId, Guid? MemberId, string Platform, string Token,
    string? DeviceIdentifier, string? AppVersion)
    : IRequest<Result<Guid>>;

public class RegisterPushDeviceCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<RegisterPushDeviceCommand, Result<Guid>>
{
    private static readonly string[] GecerliPlatformlar = ["android", "ios"];

    public async Task<Result<Guid>> Handle(RegisterPushDeviceCommand request, CancellationToken ct)
    {
        var token = request.Token?.Trim() ?? "";
        var platform = request.Platform?.Trim().ToLowerInvariant() ?? "";
        if (token.Length is < 10 or > 512)
            return Result.Failure<Guid>("Geçersiz push token.");
        if (!GecerliPlatformlar.Contains(platform))
            return Result.Failure<Guid>("Platform 'android' veya 'ios' olmalıdır.");

        var cihazKimligi = string.IsNullOrWhiteSpace(request.DeviceIdentifier)
            ? null : request.DeviceIdentifier.Trim();

        // Token rotasyonu: aynı cihazın (DeviceIdentifier) FARKLI token'lı aktif kayıtları
        // artık ölü adrestir — revoked'a çekilir ki bildirim gönderimi eski token'ı denemesin.
        if (cihazKimligi is not null)
        {
            var eskiler = await db.PushDevices
                .Where(d => d.FirmPlatformId == request.FirmPlatformId
                            && d.DeviceIdentifier == cihazKimligi
                            && d.Token != token && d.Status == "active")
                .ToListAsync(ct);
            foreach (var eski in eskiler)
            {
                eski.Status = "revoked";
                eski.RevokedAt = DateTime.UtcNow;
                eski.UpdatedAt = DateTime.UtcNow;
            }
        }

        var kayit = await db.PushDevices.FirstOrDefaultAsync(d =>
            d.FirmPlatformId == request.FirmPlatformId && d.Token == token, ct);
        if (kayit is null)
        {
            kayit = new PushDevice { FirmPlatformId = request.FirmPlatformId, Token = token };
            db.PushDevices.Add(kayit);
        }
        kayit.MemberId = request.MemberId;
        kayit.Platform = platform;
        kayit.DeviceIdentifier = cihazKimligi ?? kayit.DeviceIdentifier;
        kayit.AppVersion = string.IsNullOrWhiteSpace(request.AppVersion)
            ? kayit.AppVersion : request.AppVersion.Trim();
        kayit.Status = "active"; // yeniden kayıt revoked cihazı canlandırır
        kayit.RevokedAt = null;
        kayit.LastSeenAt = DateTime.UtcNow;
        kayit.UpdatedAt = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Çoklu node yarışı: iki istek FirstOrDefault kontrolünü aynı anda geçebilir;
            // (FirmPlatformId, Token) unique indeksi ikinci INSERT'i reddeder. Kayıt artık
            // var demektir — idempotent uç olduğundan mevcut kaydın kimliğiyle başarı dön.
            var mevcut = await db.PushDevices.AsNoTracking().FirstOrDefaultAsync(d =>
                d.FirmPlatformId == request.FirmPlatformId && d.Token == token, ct);
            if (mevcut is not null) return Result.Success(mevcut.Id);
            throw;
        }
        return Result.Success(kayit.Id);
    }
}

/// <summary>Cihaz kaydını iptal eder (çıkışta/bildirim izni kapatılınca). İdempotent:
/// kayıt yoksa da başarı döner — istemci tekrar denemek zorunda kalmaz.</summary>
public record RevokePushDeviceCommand(Guid FirmPlatformId, string Token)
    : IRequest<Result<bool>>;

public class RevokePushDeviceCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<RevokePushDeviceCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(RevokePushDeviceCommand request, CancellationToken ct)
    {
        var token = request.Token?.Trim() ?? "";
        if (token.Length == 0) return Result.Failure<bool>("Token zorunludur.");

        var kayit = await db.PushDevices.FirstOrDefaultAsync(d =>
            d.FirmPlatformId == request.FirmPlatformId && d.Token == token, ct);
        if (kayit is null || kayit.Status == "revoked") return Result.Success(true);

        kayit.Status = "revoked";
        kayit.RevokedAt = DateTime.UtcNow;
        kayit.MemberId = null; // üye bağlantısı da düşer — token artık kimseye ait değil
        kayit.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
