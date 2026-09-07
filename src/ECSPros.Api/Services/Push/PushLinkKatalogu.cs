using System.Text.RegularExpressions;

namespace ECSPros.Api.Services.Push;

/// <summary>
/// Mobil uygulamanın çözdüğü linkler (docs/PUSH_BILDIRIM_ENTEGRASYONU.md §3). Gönderim öncesi doğrulama:
/// göreli yol ya da site adresli tam URL; web'e özel yollar (/odeme, /teslimat) reddedilir.
/// </summary>
public static partial class PushLinkKatalogu
{
    static readonly string[] Yasak = ["/odeme", "/teslimat", "/api", "/admin", "/hubs"];
    static readonly string[] Hostlar = ["www.misharitalia.com", "misharitalia.com"];

    /// <summary>Geçerliyse normalize edilmiş göreli yolu döner; değilse null.</summary>
    public static string? Normalize(string? link)
    {
        if (string.IsNullOrWhiteSpace(link)) return null;
        var s = link.Trim();
        if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(s, UriKind.Absolute, out var u) || !Hostlar.Contains(u.Host, StringComparer.OrdinalIgnoreCase)) return null;
            s = u.PathAndQuery;
        }
        if (!s.StartsWith('/')) s = "/" + s;
        if (s.Length > 1) s = s.TrimEnd('/');
        var yol = s.Split('?', 2)[0];
        if (Yasak.Any(y => yol.Equals(y, StringComparison.OrdinalIgnoreCase) || yol.StartsWith(y + "/", StringComparison.OrdinalIgnoreCase))) return null;
        if (!YolBicimi().IsMatch(s)) return null;
        // /siparislerim/{id} → id GUID olmalı (sipariş numarası DEĞİL)
        var m = SiparisDetay().Match(yol);
        if (m.Success && !Guid.TryParse(m.Groups[1].Value, out _)) return null;
        return s;
    }

    [GeneratedRegex(@"^/[A-Za-z0-9\-_./%]*(\?[A-Za-z0-9\-_.=&%+ğüşıöçĞÜŞİÖÇ]*)?$")]
    private static partial Regex YolBicimi();
    [GeneratedRegex(@"^/siparislerim/([^/]+)$")]
    private static partial Regex SiparisDetay();
}
