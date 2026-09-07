using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ECSPros.Api.Services.Store;

/// <summary>
/// Arama küfür filtresi (2026-09-07, kullanıcı kararı): küfür/hakaret içeren arama terimi
/// LOGLANMAZ, ARANMAZ (boş sonuç / 400), popüler listeye GİRMEZ. Eşleşme normalize edilmiş
/// sözcük düzeyindedir (küçük harf, harf dışı karakterler atılır, 3+ tekrar harf tekile iner):
/// kısa kökler yalnız tam sözcük ("göt" evet, "götür" hayır), uzun kökler önek olarak ("orospu…").
/// Liste appsettings `Store:BlockedSearchWords` (tam sözcük) ve `Store:BlockedSearchPrefixes` ile genişletilir.
/// Kullanıcı görünen mesaj tek tiptir; hangi sözcüğün takıldığı söylenmez.
/// </summary>
public sealed class AramaKufurFiltresi
{
    public const string EngelMesaji = "Bu arama yapılamıyor.";

    private static readonly string[] VarsayilanSozcukler =
    [
        "amk", "aq", "amq", "amına", "amina", "amcık", "amcik", "orospu", "oç", "oc", "piç", "pic", "sik", "sikik", "sikim",
        "siktir", "sikerim", "sikeyim", "sikiş", "sikis", "yarrak", "yarak", "yarram", "taşak", "tasak", "göt", "got",
        "götveren", "gotveren", "ibne", "ibine", "gavat", "kahpe", "kaltak", "pezevenk", "pezeveng", "puşt", "pust",
        "meme", "memeler", "am", "döl", "dol", "sürtük", "surtuk", "fahişe", "fahise", "yavşak", "yavsak", "şerefsiz",
        "serefsiz", "gerizekalı", "gerizekali", "salak", "aptal", "dangalak", "hıyar", "hiyar", "bok", "boktan",
        "fuck", "fucking", "shit", "bitch", "dick", "pussy", "cock", "asshole", "porn", "porno", "sex", "seks", "sikişme",
        "otuzbir", "mastürbasyon", "masturbasyon", "penis", "vajina", "vagina", "anal", "oral",
    ];

    private static readonly string[] VarsayilanOnekler =
    [
        "orospu", "sikti", "siker", "sikey", "sikiş", "sikis", "yarra", "amcık", "amcik", "pezeven", "kahpe", "ibnel",
        "gavat", "götver", "gotver", "yavşa", "fahiş", "fahis", "sürtü", "surtu", "porno", "fuck",
    ];

    private readonly HashSet<string> _sozcukler;
    private readonly string[] _onekler;

    public AramaKufurFiltresi(IConfiguration configuration)
    {
        var ekSozcukler = configuration.GetSection("Store:BlockedSearchWords").Get<string[]>() ?? [];
        var ekOnekler = configuration.GetSection("Store:BlockedSearchPrefixes").Get<string[]>() ?? [];
        _sozcukler = new HashSet<string>(VarsayilanSozcukler.Concat(ekSozcukler).Select(SozcukNormalize).Where(s => s.Length > 0), StringComparer.Ordinal);
        _onekler = VarsayilanOnekler.Concat(ekOnekler).Select(SozcukNormalize).Where(s => s.Length >= 4).Distinct().ToArray();
    }

    /// <summary>Terim küfür içeriyor mu? (null/boş → false)</summary>
    public bool Engelli(string? terim)
    {
        if (string.IsNullOrWhiteSpace(terim)) return false;
        var kucuk = terim.Replace('I', 'ı').Replace('İ', 'i').ToLowerInvariant();
        // Sözcüklere böl: harf/rakam dışı her şey ayraç; ayrıca birleşik yazım için tümünü tek sözcük olarak da dene.
        var parcalar = Regex.Split(kucuk, @"[^\p{L}\p{Nd}]+").Where(p => p.Length > 0).Select(SozcukNormalize).ToList();
        var birlesik = SozcukNormalize(string.Concat(parcalar));
        foreach (var p in parcalar.Append(birlesik))
        {
            if (p.Length == 0) continue;
            if (_sozcukler.Contains(p)) return true;
            if (_onekler.Any(o => p.StartsWith(o, StringComparison.Ordinal))) return true;
        }
        return false;
    }

    /// <summary>Harf/rakam dışını at, 3+ tekrar harfi tekile indir ("sikkkk"→"sik").</summary>
    private static string SozcukNormalize(string s)
    {
        var temiz = new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return Regex.Replace(temiz, @"(\p{L})\1{2,}", "$1");
    }

    /// <summary>Popüler arama sayacı için ziyaretçi anahtarı: üye ise üye kimliği, değilse IP+UA özeti (16 hex).
    /// Kişisel veri saklanmaz; aynı kişinin tekrar aramaları tek ziyaretçi sayılır.</summary>
    public static string ZiyaretciAnahtari(HttpContext http)
    {
        if (http.User.Identity?.IsAuthenticated == true && http.User.FindFirst("type")?.Value == "member")
        {
            var sub = http.User.FindFirst("sub")?.Value ?? http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(sub)) return "m:" + sub[..Math.Min(13, sub.Length)];
        }
        var ip = http.Connection.RemoteIpAddress?.ToString() ?? "";
        var ua = http.Request.Headers.UserAgent.ToString();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(ip + "|" + ua));
        return Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant();
    }
}
