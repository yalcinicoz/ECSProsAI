using System.Text;
using System.Text.RegularExpressions;

namespace ECSPros.Storefront.Application.Helpers;

/// <summary>
/// URL slug normalizasyonu: Türkçe karakterler sadeleştirilir, [a-z0-9-] dışındaki her şey
/// (nokta, virgül, boşluk...) tireye çevrilir. Eski sistemden taşınan noktalı/virgüllü
/// URL'ler rota tarafında desteklenir ama YENİ slug'larda bu karakterlere izin verilmez
/// (kullanıcı kararı 2026-07-29).
/// </summary>
public static class UrlSlug
{
    private static readonly Dictionary<char, char> TurkceMap = new()
    {
        ['ç'] = 'c', ['Ç'] = 'c', ['ğ'] = 'g', ['Ğ'] = 'g', ['ı'] = 'i', ['İ'] = 'i',
        ['ö'] = 'o', ['Ö'] = 'o', ['ş'] = 's', ['Ş'] = 's', ['ü'] = 'u', ['Ü'] = 'u',
    };

    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
            sb.Append(TurkceMap.TryGetValue(c, out var e) ? e : c);

        return Regex.Replace(sb.ToString().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
    }
}
