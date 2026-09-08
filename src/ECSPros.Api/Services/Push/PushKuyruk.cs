using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ECSPros.Crm.Application.Services;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.Push;

/// <summary>Kuyruğa alma isteği: senaryo tipi + hedef (üye ya da tek cihaz) + yer tutucu değerleri + tekilleştirme anahtarı.</summary>
public sealed record PushIstek(
    string Type, Guid? MemberId, IReadOnlyDictionary<string, string> Vars, string DedupId,
    Guid? FirmPlatformId = null, Guid? DeviceId = null, string? ImageUrl = null,
    string? TitleOverride = null, string? BodyOverride = null, string? LinkOverride = null);

/// <summary>
/// Push kuyruğu (§5-§7): şablon → metin/link; hedef cihazlar (üyenin tüm aktif cihazları); pazarlama sınıfında izin
/// (Consents.marketing.push == false ise gitmez — opt-out), sessiz saat 22:00-09:00 (İstanbul) → 09:00'a erteleme, üye başına günde ≤2 ve aynı tip
/// haftada ≤2; (DedupId, cihaz) benzersiz. Gönderim PushGonderimServisi'nde (worker).
/// </summary>
public sealed class PushKuyruk(IStorefrontDbContext sdb, ICrmDbContext cdb, IConfiguration config, ILogger<PushKuyruk> logger)
{
    static readonly TimeZoneInfo TrTz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    public async Task<int> EnqueueAsync(PushIstek i, CancellationToken ct)
    {
        if (!config.GetValue("Push:Enabled", true)) return 0;
        var t = await sdb.PushTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Type == i.Type, ct);
        var sinif = t?.Class ?? "transactional";
        if (i.TitleOverride is null && (t is null || !t.Enabled)) { logger.LogDebug("Push şablonu yok/kapalı: {Type}", i.Type); return 0; }

        var title = Doldur(i.TitleOverride ?? t!.Title, i.Vars);
        var body = Doldur(i.BodyOverride ?? t!.Body, i.Vars);
        var link = PushLinkKatalogu.Normalize(Doldur(i.LinkOverride ?? t?.LinkTemplate ?? "/", i.Vars));
        if (link is null) { logger.LogWarning("Push linki geçersiz, gönderilmedi: {Type} {Link}", i.Type, i.LinkOverride ?? t?.LinkTemplate); return 0; }
        if (title.Length == 0 || body.Length == 0) return 0;

        // hedef cihazlar
        var cihazlar = i.DeviceId is { } did
            ? await sdb.PushDevices.AsNoTracking().Where(d => d.Id == did && d.Status == "active").ToListAsync(ct)
            : i.MemberId is { } mid
                ? await sdb.PushDevices.AsNoTracking().Where(d => d.MemberId == mid && d.Status == "active" && (i.FirmPlatformId == null || d.FirmPlatformId == i.FirmPlatformId)).ToListAsync(ct)
                : [];
        if (cihazlar.Count == 0) return 0;
        var memberId = i.MemberId ?? cihazlar[0].MemberId;

        var scheduled = DateTime.UtcNow;
        if (sinif == "marketing")
        {
            if (memberId is null || !await PazarlamaIzniVarAsync(memberId.Value, ct)) return 0;
            // günlük ≤2, aynı tip haftada ≤2 (skipped hariç)
            var bugun = IstanbulGunBaslangiciUtc(DateTime.UtcNow);
            var gunluk = await sdb.PushNotifications.CountAsync(n => n.MemberId == memberId && n.Class == "marketing" && n.Status != "skipped" && n.CreatedAt >= bugun && n.DeviceId == cihazlar[0].Id, ct);
            if (gunluk >= config.GetValue("Push:MarketingDailyLimit", 2)) return 0;
            var haftalik = await sdb.PushNotifications.CountAsync(n => n.MemberId == memberId && n.Type == i.Type && n.Status != "skipped" && n.CreatedAt >= DateTime.UtcNow.AddDays(-7) && n.DeviceId == cihazlar[0].Id, ct);
            if (haftalik >= config.GetValue("Push:MarketingWeeklySameTypeLimit", 2)) return 0;
            scheduled = SessizSaatErtele(scheduled);
        }

        var data = new Dictionary<string, string>(i.Vars.Where(kv => kv.Value.Length <= 200)) { ["type"] = i.Type, ["link"] = link, ["dedupId"] = i.DedupId };
        var mevcut = await sdb.PushNotifications.Where(n => n.DedupId == i.DedupId).Select(n => n.DeviceId).ToListAsync(ct);
        int n = 0;
        foreach (var d in cihazlar.Where(c => !mevcut.Contains(c.Id)))
        {
            sdb.PushNotifications.Add(new PushNotification
            {
                MemberId = memberId, DeviceId = d.Id, FirmPlatformId = d.FirmPlatformId, Platform = d.Platform, TokenHash = Hash(d.Token),
                Type = i.Type, Class = sinif, DedupId = i.DedupId, Title = title, Body = body, Link = link, ImageUrl = i.ImageUrl,
                Data = data, Status = "queued", ScheduledAt = scheduled,
            });
            n++;
        }
        if (n > 0) await sdb.SaveChangesAsync(ct);
        return n;
    }

    public async Task<bool> PazarlamaIzniVarAsync(Guid memberId, CancellationToken ct)
    {
        var consents = await cdb.Members.AsNoTracking().Where(m => m.Id == memberId).Select(m => m.Consents).FirstOrDefaultAsync(ct);
        return PushIzni(consents);
    }

    /// <summary>Pazarlama push izni OPT-OUT'tur (2026-09-08 kullanıcı kararı): cihaz kaydı zaten OS bildirim izniyle
    /// yapılır, ayrı bir "açma" istenmez. Yalnız üye uygulamadan kapattıysa (Consents.marketing.push == false) gitmez;
    /// anahtar yoksa/true ise gider. (jsonb → JsonElement ya da aynı süreçte yazılmış sözlük.)</summary>
    public static bool PushIzni(Dictionary<string, object>? consents)
    {
        if (consents is null || !consents.TryGetValue("marketing", out var m) || m is null) return true;
        if (m is JsonElement je && je.ValueKind == JsonValueKind.Object) return !(je.TryGetProperty("push", out var p) && p.ValueKind == JsonValueKind.False);
        if (m is Dictionary<string, object> d) return !(d.TryGetValue("push", out var v) && v is false);
        return true;
    }

    public static string Doldur(string sablon, IReadOnlyDictionary<string, string> vars)
    {
        var s = sablon;
        foreach (var (k, v) in vars) s = s.Replace("{" + k + "}", v);
        // kalan yer tutucular boşalır
        while (true) { var a = s.IndexOf('{'); if (a < 0) break; var b = s.IndexOf('}', a); if (b < 0) break; s = s.Remove(a, b - a + 1); }
        return s.Replace("  ", " ").Trim();
    }

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    static DateTime IstanbulGunBaslangiciUtc(DateTime utc)
    {
        var yerel = TimeZoneInfo.ConvertTimeFromUtc(utc, TrTz).Date;
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(yerel, DateTimeKind.Unspecified), TrTz);
    }

    /// <summary>22:00-09:00 İstanbul → bir sonraki 09:00.</summary>
    public static DateTime SessizSaatErtele(DateTime utc)
    {
        var yerel = TimeZoneInfo.ConvertTimeFromUtc(utc, TrTz);
        if (yerel.Hour >= 9 && yerel.Hour < 22) return utc;
        var hedef = yerel.Hour >= 22 ? yerel.Date.AddDays(1).AddHours(9) : yerel.Date.AddHours(9);
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(hedef, DateTimeKind.Unspecified), TrTz);
    }
}
