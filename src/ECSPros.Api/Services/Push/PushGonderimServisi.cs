using ECSPros.Crm.Application.Services;
using ECSPros.Storefront.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.Push;

/// <summary>
/// Kuyruktan gönderim (§2/§6): vadesi gelen queued satırlar → iptal koşulu → FCM → sonuç; token hatalarında cihaz durumu
/// (UNREGISTERED→revoked, INVALID_ARGUMENT→invalid); geçici hatalarda üstel bekleme (en çok 5 deneme). Rozet = üyenin
/// gönderilmiş ve açılmamış bildirim sayısı.
/// </summary>
public sealed class PushGonderimServisi(IStorefrontDbContext sdb, ICrmDbContext cdb, FcmClient fcm, DbFcmSettingsProvider ayarlar, ILogger<PushGonderimServisi> logger)
{
    public async Task<(int Sent, int Failed, int Skipped)> IsleAsync(int batch, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var kuyruk = await sdb.PushNotifications.Where(n => n.Status == "queued" && n.ScheduledAt <= now).OrderBy(n => n.ScheduledAt).Take(batch).ToListAsync(ct);
        if (kuyruk.Count == 0) return (0, 0, 0);
        var s = await ayarlar.GetAsync(ct);
        if (s is null)
        {
            logger.LogWarning("Push: FCM servis hesabı tanımlı değil (panel Firmalar › firma detayı › Entegrasyonlar › Entegrasyon Ekle › Firebase Cloud Messaging) — {N} bildirim kuyrukta bekliyor.", kuyruk.Count);
            return (0, 0, 0);
        }
        var cihazIds = kuyruk.Select(n => n.DeviceId).Distinct().ToList();
        var cihazlar = await sdb.PushDevices.Where(d => cihazIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, ct);
        int sent = 0, failed = 0, skipped = 0;
        foreach (var n in kuyruk)
        {
            if (!cihazlar.TryGetValue(n.DeviceId, out var cihaz) || cihaz.Status != "active")
            { n.Status = "skipped"; n.ErrorCode = "device_inactive"; skipped++; continue; }
            if (!await OlayHalaGecerliAsync(n, ct)) { n.Status = "skipped"; n.ErrorCode = "cancelled_condition"; skipped++; continue; }

            var badge = 1 + (n.MemberId is null ? 0 : await sdb.PushNotifications.CountAsync(x => x.MemberId == n.MemberId && x.Status == "sent" && x.OpenedAt == null && x.Id != n.Id, ct));
            var data = new Dictionary<string, string>(n.Data) { ["sentAt"] = DateTime.UtcNow.ToString("O") };
            var high = n.Class == "transactional";
            var ttl = n.Class == "transactional" ? 86400 : 43200;
            var r = await fcm.GonderAsync(s, cihaz.Token, n.Title, n.Body, n.ImageUrl, data, n.Type, ttl, high, badge, ct);
            n.Attempts++;
            if (r.Ok) { n.Status = "sent"; n.SentAt = DateTime.UtcNow; n.FcmMessageId = r.MessageId; n.ErrorCode = null; sent++; }
            else if (r.ErrorCode == "UNREGISTERED" || r.ErrorCode == "404")
            { cihaz.Status = "revoked"; cihaz.RevokedAt = DateTime.UtcNow; n.Status = "failed"; n.ErrorCode = "UNREGISTERED"; failed++; }
            else if (r.ErrorCode == "INVALID_ARGUMENT")
            { cihaz.Status = "invalid"; n.Status = "failed"; n.ErrorCode = r.ErrorCode; failed++; }
            else if (r.ErrorCode == "SENDER_ID_MISMATCH" || r.ErrorCode == "THIRD_PARTY_AUTH_ERROR" || r.ErrorCode == "PERMISSION_DENIED")
            { n.Status = "failed"; n.ErrorCode = r.ErrorCode; failed++; logger.LogError("Push yapılandırma hatası {Code}: {Detail}", r.ErrorCode, r.Detail); }
            else if (r.Retryable && n.Attempts < 5)
            { n.ScheduledAt = DateTime.UtcNow.AddSeconds(Math.Pow(2, n.Attempts) * 30); n.ErrorCode = r.ErrorCode; }
            else { n.Status = "failed"; n.ErrorCode = r.ErrorCode; failed++; logger.LogWarning("Push gönderilemedi {Type} {Code}: {Detail}", n.Type, r.ErrorCode, r.Detail); }
            if (r.ErrorCode is "429" or "QUOTA_EXCEEDED") await Task.Delay(2000, ct);
        }
        await sdb.SaveChangesAsync(ct);
        return (sent, failed, skipped);
    }

    /// <summary>İptal koşulu (§6): sepet boşaldıysa cart_*; ürün favoriden çıktıysa favorite_*; alarm iptal edildiyse stock_alert.</summary>
    async Task<bool> OlayHalaGecerliAsync(Storefront.Domain.Entities.PushNotification n, CancellationToken ct)
    {
        try
        {
            if (n.Type.StartsWith("cart_") && n.Data.TryGetValue("cartId", out var cid) && Guid.TryParse(cid, out var cartId))
                return await cdb.CartItems.AnyAsync(i => i.CartId == cartId, ct);
            if (n.Type.StartsWith("favorite_") && n.MemberId is { } mid && n.Data.TryGetValue("productCode", out var code))
                return await sdb.Favorites.AnyAsync(f => f.MemberId == mid && f.ProductCode == code, ct);
            if (n.Type == "stock_alert" && n.Data.TryGetValue("alertId", out var aid) && Guid.TryParse(aid, out var alertId))
                return await sdb.StockAlerts.AnyAsync(a => a.Id == alertId && a.Status != "cancelled", ct);
        }
        catch (Exception ex) { logger.LogDebug(ex, "iptal koşulu denetlenemedi"); }
        return true;
    }
}
