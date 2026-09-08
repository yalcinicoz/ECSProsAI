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
        // 2026-09-08 (kullanıcı kararı): üye hedeflemesinde platform başına yalnız EN SON GÖRÜLEN cihaz. Uygulama kurulumlar
        // arasında deviceId'yi sabit tutamadığında aynı telefon birden çok satır açıyor; eski token'lar bir süre FCM'de geçerli
        // kaldığından bildirim tekrarlanıyordu. Doğrudan DeviceId ile gelen istek (deneme) etkilenmez.
        if (i.DeviceId is null && cihazlar.Count > 1 && config.GetValue("Push:LatestDevicePerPlatform", true))
            cihazlar = cihazlar.GroupBy(d => d.Platform).Select(g => g.OrderByDescending(d => d.LastSeenAt).First()).ToList();
        var memberId = i.MemberId ?? (cihazlar.Count > 0 ? cihazlar[0].MemberId : null);

        // Uygulama içi liste (docs/BILDIRIMLERIM_BACKEND_ISTEGI.md §1/§6): üyeye hedeflenen bildirim FCM'e gidemese bile
        // (cihaz yok / izin yok / sıklık sınırı) Inbox=true ise cihazsız 'skipped' satırla listede görünür.
        var inbox = t?.Inbox ?? true;
        var icon = t?.Icon is { Length: > 0 } ic ? ic : BildirimKutusu.IkonVarsayilan(i.Type);
        var expiresAt = DateTime.UtcNow.AddDays(t?.ExpiresDays is > 0 ? t.ExpiresDays : (sinif == "marketing" ? 30 : 90));
        var dismissOnOpen = t?.DismissOnOpen ?? false;
        var data = new Dictionary<string, string>(i.Vars.Where(kv => kv.Value.Length <= 200)) { ["type"] = i.Type, ["link"] = link, ["dedupId"] = i.DedupId };
        var platformId = i.FirmPlatformId ?? (cihazlar.Count > 0 ? cihazlar[0].FirmPlatformId : Guid.Empty);
        if (platformId == Guid.Empty && memberId is { } mIdPlatform)   // cihazsız listeye giren satır: üyenin son kayıtlı cihazının (durumu ne olursa olsun) platformu
            platformId = await sdb.PushDevices.AsNoTracking().Where(d => d.MemberId == mIdPlatform).OrderByDescending(d => d.LastSeenAt).Select(d => d.FirmPlatformId).FirstOrDefaultAsync(ct);
        PushNotification Satir(PushDevice? d, string status, string? hata, DateTime scheduled) => new()
        {
            MemberId = memberId, DeviceId = d?.Id, FirmPlatformId = d?.FirmPlatformId ?? platformId, Platform = d?.Platform ?? "inbox",
            TokenHash = d is null ? "" : Hash(d.Token), Type = i.Type, Class = sinif, DedupId = i.DedupId, Title = title, Body = body, Link = link,
            ImageUrl = i.ImageUrl, Data = data, Status = status, ErrorCode = hata, ScheduledAt = scheduled,
            Inbox = inbox, Icon = icon, ExpiresAt = expiresAt, DismissOnOpen = dismissOnOpen,
        };
        async Task<int> YalnizListeyeAsync(string neden)
        {
            if (!inbox || memberId is null || platformId == Guid.Empty) return 0;
            if (await sdb.PushNotifications.AnyAsync(x => x.DedupId == i.DedupId && x.MemberId == memberId, ct)) return 0;   // aynı olay zaten var
            sdb.PushNotifications.Add(Satir(null, "skipped", neden, DateTime.UtcNow));
            await sdb.SaveChangesAsync(ct);
            return 0;   // push gönderilmedi (dönüş değeri gönderim adedi)
        }
        if (cihazlar.Count == 0) return await YalnizListeyeAsync("no_device");

        var scheduled = DateTime.UtcNow;
        if (sinif == "marketing")
        {
            if (memberId is null) return 0;
            if (!await PazarlamaIzniVarAsync(memberId.Value, ct)) return await YalnizListeyeAsync("no_consent");
            // günlük ≤2, aynı tip haftada ≤2 (skipped hariç)
            var bugun = IstanbulGunBaslangiciUtc(DateTime.UtcNow);
            var gunluk = await sdb.PushNotifications.CountAsync(n => n.MemberId == memberId && n.Class == "marketing" && n.Status != "skipped" && n.CreatedAt >= bugun && n.DeviceId == cihazlar[0].Id, ct);
            if (gunluk >= config.GetValue("Push:MarketingDailyLimit", 2)) return await YalnizListeyeAsync("daily_limit");
            var haftalik = await sdb.PushNotifications.CountAsync(n => n.MemberId == memberId && n.Type == i.Type && n.Status != "skipped" && n.CreatedAt >= DateTime.UtcNow.AddDays(-7) && n.DeviceId == cihazlar[0].Id, ct);
            if (haftalik >= config.GetValue("Push:MarketingWeeklySameTypeLimit", 2)) return await YalnizListeyeAsync("weekly_limit");
            scheduled = SessizSaatErtele(scheduled);
        }

        var mevcut = await sdb.PushNotifications.Where(n => n.DedupId == i.DedupId && n.DeviceId != null).Select(n => n.DeviceId!.Value).ToListAsync(ct);
        int n = 0;
        foreach (var d in cihazlar.Where(c => !mevcut.Contains(c.Id)))
        {
            sdb.PushNotifications.Add(Satir(d, "queued", null, scheduled));
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
