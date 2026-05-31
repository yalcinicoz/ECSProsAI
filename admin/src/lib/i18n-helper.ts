/**
 * i18n-helper — Cinsiyet bağlamına duyarlı çok dilli yardımcı fonksiyonlar.
 *
 * Anahtar formatı: "{lang}" veya "{lang}:{cinsiyet}"
 *   Örnek: { "tr": "Pantolon", "ar": "بنطلون", "ar:m": "بنطلون رجالي", "ar:f": "بنطلون نسائي" }
 */

/** Cinsiyet varyantı destekleyen dil kodları */
export const GENDER_AWARE_LANGS: ReadonlySet<string> = new Set(['ar', 'he'])

/**
 * Çok dilli sözlükten en uygun değeri döndürür.
 * Öncelik: lang:genderCtx → lang → fallback → ilk değer
 */
export function getLocalized(
  i18n: Record<string, string> | undefined,
  lang: string,
  genderCtx?: 'm' | 'f' | null,
  fallback = 'tr',
): string {
  if (!i18n) return ''
  if (genderCtx && i18n[`${lang}:${genderCtx}`]) return i18n[`${lang}:${genderCtx}`]
  if (i18n[lang]) return i18n[lang]
  if (i18n[fallback]) return i18n[fallback]
  return Object.values(i18n)[0] ?? ''
}

/**
 * I18nField bileşeni için değer haritası oluşturur.
 * Cinsiyet farkında diller için "lang:m" ve "lang:f" pseudo-anahtarlarını da içerir.
 *
 * @param nameI18n  Flat dict: { tr: "Pantolon", ar: "بنطلون", "ar:m": "...", "ar:f": "..." }
 * @param languages Dil listesi (genderAware flag içerir)
 * @param fieldKey  Tek alan anahtarı (varsayılan: "name")
 */
export function buildI18nValues(
  nameI18n: Record<string, string>,
  languages: ReadonlyArray<{ code: string; genderAware?: boolean }>,
  fieldKey = 'name',
): Record<string, Record<string, string>> {
  const result: Record<string, Record<string, string>> = {}
  for (const l of languages) {
    result[l.code] = { [fieldKey]: nameI18n[l.code] ?? '' }
    if (l.genderAware) {
      result[`${l.code}:m`] = { [fieldKey]: nameI18n[`${l.code}:m`] ?? '' }
      result[`${l.code}:f`] = { [fieldKey]: nameI18n[`${l.code}:f`] ?? '' }
    }
  }
  return result
}
