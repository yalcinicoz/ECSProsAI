namespace ECSPros.Shared.Kernel.Common;

/// <summary>
/// Çok dilli sözlüklerden dil + cinsiyet bağlamına göre değer döndüren yardımcı.
/// Anahtar formatı: "{lang}" veya "{lang}:{cinsiyet}" (örn. "ar:m", "ar:f")
/// </summary>
public static class I18nHelper
{
    /// <summary>
    /// Cinsiyet varyantı destekleyen dil kodları.
    /// </summary>
    public static readonly IReadOnlySet<string> GenderAwareLangs =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ar", "he" };

    /// <summary>
    /// Sözlükten en uygun değeri döndürür.
    /// Öncelik: lang:genderCtx → lang → fallbackLang → ilk değer
    /// </summary>
    /// <param name="i18n">Flat sözlük: {"tr":"Pantolon","ar":"بنطلون","ar:f":"بنطال"}</param>
    /// <param name="lang">İstenen dil kodu (örn. "ar")</param>
    /// <param name="genderCtx">"m" | "f" | null</param>
    /// <param name="fallbackLang">Yedek dil (varsayılan: "tr")</param>
    public static string GetLocalized(
        Dictionary<string, string>? i18n,
        string lang,
        string? genderCtx = null,
        string fallbackLang = "tr")
    {
        if (i18n is null || i18n.Count == 0) return string.Empty;

        // 1. lang:gender → "ar:f"
        if (!string.IsNullOrEmpty(genderCtx) &&
            i18n.TryGetValue($"{lang}:{genderCtx}", out var genVal))
            return genVal;

        // 2. lang → "ar"
        if (i18n.TryGetValue(lang, out var langVal))
            return langVal;

        // 3. fallback dil
        if (i18n.TryGetValue(fallbackLang, out var fbVal))
            return fbVal;

        // 4. ilk değer
        return i18n.Values.FirstOrDefault() ?? string.Empty;
    }
}
