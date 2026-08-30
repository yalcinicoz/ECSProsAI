using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ECSPros.Shared.Contracts.Tracking;

namespace ECSPros.Api.Services.Tracking;

/// <summary>
/// HTTP isteğinden tarayıcı eşleştirme bağlamı + consent okur (İE-2 Faz B). Çerezler:
/// _fbp/_fbc (Meta), _ga (GA4 client_id: "GA1.1.X.Y" → "X.Y"), _ttp/ttclid (TikTok),
/// _gcl_aw/gclid (Google Ads), ms_consent (Faz C consent banner'ı yazar; yoksa DENY — EU kararı).
/// PII yalnız hash'lenmiş biçimde üretilir (<see cref="Sha256"/>).
/// </summary>
public static class TrackingHttpContextReader
{
    public const string ConsentCookie = "ms_consent";

    public static ClientContext ReadClient(HttpContext? http, string? email = null, string? phone = null, Guid? memberId = null)
    {
        if (http is null)
            return ClientContext.Bos with { EmailSha256 = Sha256(NormalizeEmail(email)), PhoneSha256 = Sha256(NormalizePhone(phone)),
                ExternalIdSha256 = memberId is { } m ? Sha256(m.ToString("D")) : null };

        var req = http.Request;
        var cookies = req.Cookies;
        string? Cookie(string ad) => cookies.TryGetValue(ad, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

        // _fbc yoksa fbclid query'den türetilir (Meta kuralı: fb.1.<ts>.<fbclid>)
        var fbc = Cookie("_fbc");
        if (fbc is null && req.Query.TryGetValue("fbclid", out var fbclid) && !string.IsNullOrWhiteSpace(fbclid))
            fbc = $"fbc.1.{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.{fbclid}".Replace("fbc.", "fb.");

        var ga = Cookie("_ga");
        string? gaClientId = null;
        if (ga is not null)
        {
            var p = ga.Split('.');
            gaClientId = p.Length >= 4 ? $"{p[^2]}.{p[^1]}" : ga;
        }

        var ttclid = req.Query.TryGetValue("ttclid", out var tq) && !string.IsNullOrWhiteSpace(tq) ? tq.ToString() : Cookie("ttclid");
        var gclid = req.Query.TryGetValue("gclid", out var gq) && !string.IsNullOrWhiteSpace(gq) ? gq.ToString() : Cookie("_gcl_aw");
        if (gclid is not null && gclid.StartsWith("GCL.", StringComparison.Ordinal))
        {
            var parts = gclid.Split('.');
            gclid = parts.Length >= 3 ? parts[^1] : gclid;
        }

        var ua = req.Headers.UserAgent.ToString();
        var url = $"{req.Scheme}://{req.Host}{req.Path}{req.QueryString}";
        var referrer = req.Headers.Referer.ToString();

        return new ClientContext(
            Ip: IstemciIp(http),
            UserAgent: string.IsNullOrWhiteSpace(ua) ? null : ua,
            Fbp: Cookie("_fbp"),
            Fbc: fbc,
            GaClientId: gaClientId,
            TtClickId: ttclid,
            Gclid: gclid,
            PageUrl: url.Length > 2000 ? url[..2000] : url,
            Referrer: string.IsNullOrWhiteSpace(referrer) ? null : referrer,
            EmailSha256: Sha256(NormalizeEmail(email)),
            PhoneSha256: Sha256(NormalizePhone(phone)),
            ExternalIdSha256: memberId is { } mid ? Sha256(mid.ToString("D")) : null);
    }

    /// <summary>ms_consent çerezi: {"v":1,"analytics":bool,"ads":bool,"personalization":bool,"ts":...}
    /// (Faz C banner'ı yazar). Yoksa/bozuksa DENY (EU kararı 2026-08-22).</summary>
    public static ConsentState ReadConsent(HttpContext? http)
    {
        if (http is null || !http.Request.Cookies.TryGetValue(ConsentCookie, out var raw) || string.IsNullOrWhiteSpace(raw))
            return ConsentState.Deny;
        try
        {
            var json = Uri.UnescapeDataString(raw);
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            bool B(string k) => r.TryGetProperty(k, out var v) && (v.ValueKind == JsonValueKind.True || (v.ValueKind == JsonValueKind.String && v.GetString() == "true"));
            return new ConsentState(B("analytics"), B("ads"), B("personalization"));
        }
        catch
        {
            return ConsentState.Deny;
        }
    }

    public static string IstemciIp(HttpContext http)
    {
        // FAZ 11 / K1: ForwardedHeadersMiddleware yalnız güvenilir proxy zincirini
        // işledikten sonra RemoteIpAddress gerçek istemciyi temsil eder.
        return http.Connection.RemoteIpAddress?.ToString() ?? "";
    }

    public static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var e = email.Trim().ToLowerInvariant();
        return e.Contains('@') ? e : null;
    }

    /// <summary>E.164 (artı işaretsiz): "0532 123 45 67" → "905321234567"; 10 hane → 90 öneki.</summary>
    public static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return null;
        if (digits.Length == 11 && digits.StartsWith('0')) digits = "90" + digits[1..];
        else if (digits.Length == 10) digits = "90" + digits;
        return digits.Length is >= 10 and <= 15 ? digits : null;
    }

    public static string? Sha256(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
