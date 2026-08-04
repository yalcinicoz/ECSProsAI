using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ECSPros.Core.Application.Services;
using ECSPros.Order.Application.Commands.ConfirmOrder;
using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Infrastructure.Messaging;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Api.Services.Store;

/// <summary>
/// Sipariş onay akışı (O2, 2026-08-04 — kullanıcı kararları): onay YENİ sitede alınır;
/// eskiye yalnız onaylı sipariş "Hazırlanıyor" gider. Politika (panel Bildirim Şablonları
/// ekranından, FirmPlatform.Settings): kapıda=always|never (vars. always), kart=
/// first_order|always|never (vars. first_order — misafir ya da önceki olumlu siparişi
/// olmayan üye onaya düşer). Onay linki token'ı HASH'lenerek saklanır, ömrü
/// orderConfirmLinkHours (vars. 24 saat); SMS her zaman, e-posta adresi varsa e-posta.
/// Şablonlar core.notification_templates'tan (tip kodu siparis_onay), yoksa gömülü
/// varsayılanlar. TÜM gönderimler hata-güvenli: SMS/e-posta gitmese sipariş akışı bozulmaz.
/// </summary>
public interface IOrderConfirmationService
{
    /// <summary>Checkout (kapıda) / PayTR paid (kart) sonrası: politika onay istiyorsa
    /// token üretir ve SMS(+e-posta) gönderir. Hata-güvenli.</summary>
    Task SiparisSonrasiBaslatAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>PayTR callback ön kararı: kart siparişinde onay gerekli mi?
    /// (true → otomatik confirm YAPILMAZ, onay linki gönderilir.)</summary>
    Task<bool> KartOnayGerekliMiAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>Linkteki token ile onay. Durum: onaylandi | zaten-onayli | suresi-doldu |
    /// bulunamadi | durum-uygun-degil.</summary>
    Task<OnaySonucu> TokenlaOnaylaAsync(string token, CancellationToken ct = default);

    /// <summary>Üye Siparişlerim'den onay (sahiplik çağıranda denetlenir).</summary>
    Task<OnaySonucu> SiteOnaylaAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>Süresi dolan link için yeni token üret + yeniden gönder (2 dk yeniden-gönderim freni).</summary>
    Task<bool> YenidenGonderAsync(string eskiToken, CancellationToken ct = default);
}

public sealed record OnaySonucu(string Durum, string? OrderNumber = null);

public sealed class OrderConfirmationService(
    IOrderDbContext orderDb,
    ECSPros.Crm.Application.Services.ICrmDbContext crmDb,
    ICoreDbContext coreDb,
    ISmsService sms,
    IEmailService email,
    IMediator mediator,
    IConfiguration config,
    IMemoryCache cache,
    ILogger<OrderConfirmationService> logger) : IOrderConfirmationService
{
    public const string TipKodu = "siparis_onay";

    // ── Gömülü varsayılan şablonlar (DB'de yoksa) — panel ekranı bunları önceden doldurur ──
    public const string VarsayilanSmsBody =
        "Sayin {ad} {soyad}, {siparisNo} nolu siparisinizi onaylamak icin: {link} (Link {sure} saat gecerlidir.)";
    public const string VarsayilanEmailKonu = "{siparisNo} — Siparişinizi Onaylayın";
    public const string VarsayilanEmailBody =
        "<p>Sayın {ad} {soyad},</p><p>{siparisNo} numaralı {tutar} TL tutarındaki siparişinizi " +
        "onaylamak için <a href=\"{link}\">buraya tıklayın</a>.</p>" +
        "<p>Bağlantı {sure} saat geçerlidir. Siparişi siz vermediyseniz bu iletiyi yok sayabilirsiniz.</p>";

    // ─── Politika ────────────────────────────────────────────────────────
    private sealed record Politika(string Cod, string Card, int LinkSaat);

    private async Task<Politika> PolitikaAsync(Guid firmPlatformId, CancellationToken ct)
    {
        var anahtar = $"order-confirm-policy:{firmPlatformId:N}";
        if (cache.TryGetValue(anahtar, out Politika? p) && p is not null) return p;

        var settings = await coreDb.FirmPlatforms.AsNoTracking()
            .Where(x => x.Id == firmPlatformId).Select(x => x.Settings).FirstOrDefaultAsync(ct);
        string cod = "always", card = "first_order";
        var saat = 24;
        if (settings is not null)
        {
            if (settings.TryGetValue("orderConfirmPolicy", out var po)
                && po is JsonElement { ValueKind: JsonValueKind.Object } je)
            {
                if (je.TryGetProperty("cod", out var c) && c.ValueKind == JsonValueKind.String) cod = c.GetString()!;
                if (je.TryGetProperty("card", out var k) && k.ValueKind == JsonValueKind.String) card = k.GetString()!;
            }
            if (settings.TryGetValue("orderConfirmLinkHours", out var h)
                && h is JsonElement { ValueKind: JsonValueKind.Number } hj && hj.GetInt32() > 0)
                saat = hj.GetInt32();
        }
        var sonuc = new Politika(cod, card, saat);
        cache.Set(anahtar, sonuc, TimeSpan.FromMinutes(1));
        return sonuc;
    }

    public async Task<bool> KartOnayGerekliMiAsync(Guid orderId, CancellationToken ct = default)
    {
        try
        {
            var o = await orderDb.Orders.AsNoTracking()
                .Where(x => x.Id == orderId)
                .Select(x => new { x.FirmPlatformId, x.MemberId })
                .FirstOrDefaultAsync(ct);
            if (o is null) return false;
            var p = await PolitikaAsync(o.FirmPlatformId, ct);
            return p.Card switch
            {
                "never" => false,
                "always" => true,
                _ => await IlkSiparisMiAsync(orderId, o.MemberId, ct) // first_order
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Kart onay politikası değerlendirilemedi (orderId={Id}) — otomatik onaya düşülüyor.", orderId);
            return false; // emniyet: politika okunamazsa mevcut davranış (otomatik onay)
        }
    }

    /// <summary>Misafir → her zaman onay; üye → bu sipariş dışında olumlu (iptal/başarısız
    /// olmayan) siparişi yoksa onay.</summary>
    private async Task<bool> IlkSiparisMiAsync(Guid orderId, Guid? memberId, CancellationToken ct)
    {
        if (memberId is null) return true;
        var olumlu = await orderDb.Orders.AsNoTracking().CountAsync(x =>
            x.MemberId == memberId && x.Id != orderId
            && (x.Status == "confirmed" || x.Status == "processing" || x.Status == "shipped" || x.Status == "delivered"), ct);
        return olumlu == 0;
    }

    // ─── Başlatma + gönderim ─────────────────────────────────────────────
    public async Task SiparisSonrasiBaslatAsync(Guid orderId, CancellationToken ct = default)
    {
        try
        {
            var order = await orderDb.Orders.FirstOrDefaultAsync(x => x.Id == orderId, ct);
            if (order is null || order.Status != "pending") return;

            var p = await PolitikaAsync(order.FirmPlatformId, ct);
            var kapida = order.PaymentMethod is "kapida-nakit" or "kapida-kart";
            var gerekli = kapida
                ? p.Cod != "never"
                : p.Card switch
                {
                    "never" => false,
                    "always" => true,
                    _ => await IlkSiparisMiAsync(orderId, order.MemberId, ct)
                };
            if (!gerekli) return;

            await TokenUretVeGonderAsync(order, p, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sipariş onay gönderimi başarısız (orderId={Id}) — sipariş akışı etkilenmez.", orderId);
        }
    }

    private async Task TokenUretVeGonderAsync(
        ECSPros.Order.Domain.Entities.Order order, Politika p, CancellationToken ct)
    {
        // Ham token yalnız linkte — DB'de SHA256 hex
        var hamToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var kayit = new OrderConfirmation
        {
            OrderId = order.Id,
            TokenHash = TokenHashle(hamToken),
            ExpiresAt = DateTime.UtcNow.AddHours(p.LinkSaat)
        };
        orderDb.OrderConfirmations.Add(kayit);
        await orderDb.SaveChangesAsync(ct);

        var link = $"{PublicBaseUrl(order.FirmPlatformId)}/o/{hamToken}";
        var (ad, soyad) = AdSoyadAyir(order.ShippingRecipientName);
        string? uyeEposta = null, uyeTelefon = null;
        if (order.MemberId is { } mid)
        {
            var uye = await crmDb.Members.AsNoTracking()
                .Where(m => m.Id == mid).Select(m => new { m.Email, m.Phone }).FirstOrDefaultAsync(ct);
            uyeEposta = uye?.Email; uyeTelefon = uye?.Phone;
        }

        var degiskenler = new Dictionary<string, string>
        {
            ["ad"] = ad, ["soyad"] = soyad,
            ["siparisNo"] = order.OrderNumber,
            ["tutar"] = order.GrandTotal.ToString("N2", new System.Globalization.CultureInfo("tr-TR")),
            ["link"] = link, ["sure"] = p.LinkSaat.ToString()
        };

        // SMS (telefon: teslimat alıcısı, yoksa üye) — hata-güvenli
        var telefon = string.IsNullOrWhiteSpace(order.ShippingRecipientPhone) ? uyeTelefon : order.ShippingRecipientPhone;
        if (!string.IsNullOrWhiteSpace(telefon))
        {
            try
            {
                var govde = await SablonAsync("sms", ct);
                await sms.SendAsync(telefon!, Doldur(govde.body, degiskenler), ct);
                kayit.SmsSentAt = DateTime.UtcNow;
            }
            catch (Exception ex) { logger.LogWarning(ex, "Onay SMS gönderilemedi ({No}).", order.OrderNumber); }
        }

        // E-posta (varsa) — hata-güvenli
        if (!string.IsNullOrWhiteSpace(uyeEposta))
        {
            try
            {
                var sablon = await SablonAsync("email", ct);
                await email.SendAsync(uyeEposta!, Doldur(sablon.subject ?? VarsayilanEmailKonu, degiskenler),
                    Doldur(sablon.body, degiskenler), ct);
                kayit.EmailSentAt = DateTime.UtcNow;
            }
            catch (Exception ex) { logger.LogWarning(ex, "Onay e-postası gönderilemedi ({No}).", order.OrderNumber); }
        }

        await orderDb.SaveChangesAsync(ct);
        logger.LogInformation("Sipariş onay linki gönderildi: {No} (sms={Sms}, email={Email}, ömür={Saat}h)",
            order.OrderNumber, kayit.SmsSentAt is not null, kayit.EmailSentAt is not null, p.LinkSaat);
    }

    // ─── Onaylama ────────────────────────────────────────────────────────
    public async Task<OnaySonucu> TokenlaOnaylaAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 64) return new("bulunamadi");
        var kayit = await orderDb.OrderConfirmations
            .FirstOrDefaultAsync(x => x.TokenHash == TokenHashle(token.Trim()), ct);
        if (kayit is null) return new("bulunamadi");

        var order = await orderDb.Orders.AsNoTracking()
            .Where(x => x.Id == kayit.OrderId)
            .Select(x => new { x.OrderNumber, x.Status })
            .FirstOrDefaultAsync(ct);
        if (order is null) return new("bulunamadi");

        if (kayit.ConfirmedAt is not null || order.Status is "confirmed" or "processing" or "shipped" or "delivered")
            return new("zaten-onayli", order.OrderNumber);
        if (order.Status != "pending") return new("durum-uygun-degil", order.OrderNumber);
        if (kayit.ExpiresAt < DateTime.UtcNow) return new("suresi-doldu", order.OrderNumber);

        return await OnayiUygulaAsync(kayit, order.OrderNumber, "link", ct);
    }

    public async Task<OnaySonucu> SiteOnaylaAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await orderDb.Orders.AsNoTracking()
            .Where(x => x.Id == orderId).Select(x => new { x.OrderNumber, x.Status }).FirstOrDefaultAsync(ct);
        if (order is null) return new("bulunamadi");
        if (order.Status is "confirmed" or "processing" or "shipped" or "delivered")
            return new("zaten-onayli", order.OrderNumber);
        if (order.Status != "pending") return new("durum-uygun-degil", order.OrderNumber);

        var kayit = await orderDb.OrderConfirmations
            .Where(x => x.OrderId == orderId && x.ConfirmedAt == null)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        return await OnayiUygulaAsync(kayit, order.OrderNumber, "site", ct, orderId);
    }

    private async Task<OnaySonucu> OnayiUygulaAsync(
        OrderConfirmation? kayit, string orderNumber, string yol, CancellationToken ct, Guid? orderId = null)
    {
        var hedefOrderId = kayit?.OrderId ?? orderId!.Value;
        var sonuc = await mediator.Send(new ConfirmOrderCommand(hedefOrderId, Guid.Empty, Guid.Empty), ct);
        if (sonuc.IsFailure)
        {
            logger.LogWarning("Sipariş onayı uygulanamadı ({No}): {Hata}", orderNumber, sonuc.Error);
            return new("durum-uygun-degil", orderNumber);
        }
        if (kayit is not null)
        {
            kayit.ConfirmedAt = DateTime.UtcNow;
            kayit.ConfirmedVia = yol;
            await orderDb.SaveChangesAsync(ct);
        }
        logger.LogInformation("Sipariş müşteri tarafından onaylandı: {No} ({Yol})", orderNumber, yol);
        return new("onaylandi", orderNumber);
    }

    public async Task<bool> YenidenGonderAsync(string eskiToken, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(eskiToken) || eskiToken.Length > 64) return false;
            var kayit = await orderDb.OrderConfirmations
                .FirstOrDefaultAsync(x => x.TokenHash == TokenHashle(eskiToken.Trim()), ct);
            if (kayit is null || kayit.ConfirmedAt is not null) return false;

            var order = await orderDb.Orders.FirstOrDefaultAsync(x => x.Id == kayit.OrderId, ct);
            if (order is null || order.Status != "pending") return false;

            // Yeniden-gönderim freni: 2 dk içinde ikinci gönderim yok (SMS maliyeti/istismar)
            var sonGonderim = await orderDb.OrderConfirmations
                .Where(x => x.OrderId == order.Id)
                .MaxAsync(x => (DateTime?)x.CreatedAt, ct);
            if (sonGonderim is { } sg && DateTime.UtcNow - sg < TimeSpan.FromMinutes(2)) return true;

            kayit.IsDeleted = true; // eski link geçersizleşir
            var p = await PolitikaAsync(order.FirmPlatformId, ct);
            await TokenUretVeGonderAsync(order, p, ct);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Onay linki yeniden gönderilemedi.");
            return false;
        }
    }

    // ─── Şablon + yardımcılar ────────────────────────────────────────────
    private async Task<(string? subject, string body)> SablonAsync(string kanal, CancellationToken ct)
    {
        try
        {
            var sablon = await coreDb.NotificationTemplates.AsNoTracking()
                .Where(t => t.IsActive && t.Channel == kanal && t.LanguageCode == "tr"
                         && t.NotificationType.Code == TipKodu)
                .Select(t => new { t.Subject, t.Body })
                .FirstOrDefaultAsync(ct);
            if (sablon is not null && !string.IsNullOrWhiteSpace(sablon.Body))
                return (sablon.Subject, sablon.Body);
        }
        catch (Exception ex) { logger.LogWarning(ex, "Bildirim şablonu okunamadı ({Kanal}) — varsayılan kullanılacak.", kanal); }
        return kanal == "sms" ? (null, VarsayilanSmsBody) : (VarsayilanEmailKonu, VarsayilanEmailBody);
    }

    private static string Doldur(string sablon, Dictionary<string, string> degiskenler)
    {
        foreach (var (k, v) in degiskenler) sablon = sablon.Replace("{" + k + "}", v);
        return sablon;
    }

    private string PublicBaseUrl(Guid firmPlatformId)
    {
        // Store:Hosts { "host": "platformCode" } ters eşlemesi; bulunamazsa PublicBaseUrl config'i
        try
        {
            var kod = coreDb.FirmPlatforms.AsNoTracking()
                .Where(p => p.Id == firmPlatformId).Select(p => p.Code).FirstOrDefault();
            var hosts = config.GetSection("Store:Hosts").Get<Dictionary<string, string>>() ?? new();
            var host = hosts.FirstOrDefault(kv => kv.Value == kod).Key;
            if (!string.IsNullOrWhiteSpace(host)) return $"https://{host}";
        }
        catch { /* fallback */ }
        return (config["Store:PublicBaseUrl"] ?? "https://new.ecspros.com").TrimEnd('/');
    }

    private static string TokenHashle(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static (string, string) AdSoyadAyir(string tam)
    {
        tam = (tam ?? "").Trim();
        var i = tam.LastIndexOf(' ');
        return i < 0 ? (tam, "") : (tam[..i], tam[(i + 1)..]);
    }
}
