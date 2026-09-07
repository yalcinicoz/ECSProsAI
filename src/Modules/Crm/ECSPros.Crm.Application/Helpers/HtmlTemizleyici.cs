using System.Net;
using System.Text.RegularExpressions;

namespace ECSPros.Crm.Application.Helpers;

/// <summary>
/// Panel zengin metin (Quill) içeriği için hafif temizlik: script/style/iframe/form etiketleri, on* olay öznitelikleri ve
/// javascript: adresleri atılır; düz metin sürümü arama için üretilir. Eski sistem hiç temizlemiyordu (XSS'e açıktı);
/// içerik personelden gelse de panelde ham basıldığı için burada süzülür.
/// </summary>
public static partial class HtmlTemizleyici
{
    public static string Temizle(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var s = TehlikeliBlok().Replace(html, string.Empty);
        s = OlayOzniteligi().Replace(s, string.Empty);
        s = JsAdres().Replace(s, "$1\"#\"");
        return s.Trim();
    }

    public static string DuzMetin(string? html, int maxLen = 5000)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var s = TehlikeliBlok().Replace(html, " ");
        s = BlokSonu().Replace(s, " ");
        s = Etiket().Replace(s, string.Empty);
        s = WebUtility.HtmlDecode(s).Replace(' ', ' ');
        s = Bosluk().Replace(s, " ").Trim();
        return s.Length > maxLen ? s[..maxLen] : s;
    }

    [GeneratedRegex(@"<(script|style|iframe|object|embed|form)\b[^>]*>.*?</\1\s*>|<(script|style|iframe|object|embed|form)\b[^>]*/?>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TehlikeliBlok();
    [GeneratedRegex(@"\s+on[a-z]+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase)]
    private static partial Regex OlayOzniteligi();
    [GeneratedRegex(@"((?:href|src)\s*=\s*)[""']?\s*javascript:[^""'\s>]*[""']?", RegexOptions.IgnoreCase)]
    private static partial Regex JsAdres();
    [GeneratedRegex(@"</(p|div|br|li|tr|h[1-6]|td)\s*>|<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BlokSonu();
    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Etiket();
    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex Bosluk();
}
