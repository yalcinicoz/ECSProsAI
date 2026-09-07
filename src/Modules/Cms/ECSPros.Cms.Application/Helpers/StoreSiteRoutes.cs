namespace ECSPros.Cms.Application.Helpers;

/// <summary>
/// B7 (2026-09-07, mobil): CMS sayfa kodu ↔ sitedeki URL slug'ı ve takma adlar. Kurumsal sayfalar
/// "kurumsal-" öneki taşır (legal kodlarla çakışmasın), site URL'leri ise "kargo-ve-teslimat" gibidir;
/// istemci artık normalizasyon yapmaz — kod, site slug'ı ya da takma adla sorar, yanıtta <c>slug</c> gelir.
/// Çözümde ÖNCE tam kod eşleşmesi (legal "gizlilik-guvenlik" gibi), sonra slug/takma ad aranır.
/// Razor rotaları: KurumsalController.
/// </summary>
public static class StoreSiteRoutes
{
    public sealed record Route(string Code, string PageType, string Slug, string[] Aliases);

    public static readonly IReadOnlyList<Route> Routes =
    [
        new("kurumsal-hakkimizda",         "corporate", "hakkimizda",           []),
        new("kurumsal-kargo-teslimat",     "corporate", "kargo-ve-teslimat",    ["kargo-teslimat"]),
        new("kurumsal-iade-degisim",       "corporate", "iade-ve-degisim",      ["iade-degisim"]),
        new("kurumsal-sss",                "corporate", "sik-sorulan-sorular",  ["sss"]),
        new("kurumsal-kullanim-kosullari", "corporate", "kullanim-kosullari",   []),
        new("kurumsal-gizlilik-guvenlik",  "corporate", "gizlilik-ve-guvenlik", ["gizlilik-guvenlik"]),
    ];

    private static readonly Dictionary<string, Route> ByCode =
        Routes.ToDictionary(r => r.Code, StringComparer.OrdinalIgnoreCase);

    /// <summary>Sayfa kodunun site slug'ı (yoksa kodun kendisi).</summary>
    public static string SlugFor(string code) => ByCode.TryGetValue(code, out var r) ? r.Slug : code;

    public static string[] AliasesFor(string code) => ByCode.TryGetValue(code, out var r) ? r.Aliases : [];

    /// <summary>Kod / site slug'ı / takma ad → (kod, sayfa tipi). Bilinen kodlar tam eşleşmede öncelikli;
    /// eşleşme yoksa girdinin kendisi kod sayılır (tip null = bilinmiyor).</summary>
    public static (string Code, string? PageType) Resolve(string codeOrSlug, ISet<string>? knownCodes = null)
    {
        var s = codeOrSlug.Trim().Trim('/');
        if (knownCodes is not null && knownCodes.Contains(s)) return (s, null);
        if (ByCode.TryGetValue(s, out var byCode)) return (byCode.Code, byCode.PageType);
        var r = Routes.FirstOrDefault(x =>
            string.Equals(x.Slug, s, StringComparison.OrdinalIgnoreCase)
            || x.Aliases.Any(a => string.Equals(a, s, StringComparison.OrdinalIgnoreCase)));
        return r is null ? (s, null) : (r.Code, r.PageType);
    }
}
