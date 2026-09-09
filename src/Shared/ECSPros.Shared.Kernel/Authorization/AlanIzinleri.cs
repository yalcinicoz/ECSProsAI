namespace ECSPros.Shared.Kernel.Authorization;

/// <summary>
/// Y6 (2026-09-09, karar K6): HASSAS ALAN izinleri — v1'de yalnız beş alan.
///
/// Kural (tasarım §C.3): yetkisi olmayana veri ÜRETİLMEZ. Alan DTO'dan kaldırılmaz (istemci
/// sözleşmesi bozulmasın), <see cref="Gizli"/> maskesiyle ya da null olarak döner. Aynı kural
/// export kolonlarında ve yazdırma yüzeylerinde de geçerlidir.
///
/// Bu tip bilinçli olarak "izin verilenler" listesidir: varsayılan kurucusu HER ŞEYİ KAPALI
/// üretir (default deny) — bir çağıran izinleri geçirmeyi unutursa veri sızmaz, maskelenir.
///
/// ★ CACHE KURALI (tasarım §O.7): maskeleme İSTEK BAŞINA uygulanır. Hassas alan taşıyan bir
/// yanıtı paylaşımlı önbelleğe (Redis/IMemoryCache) koyacaksan izinleri CACHE ANAHTARINA ekle;
/// aksi hâlde yetkili kullanıcının önbelleği yetkisize servis edilir. 2026-09-09 taraması:
/// hassas alan döndüren hiçbir panel ucu cache'lenmiyordu (cache'li tek yer vitrin listeleri).
/// </summary>
public readonly record struct AlanIzinleri(
    bool Maliyet = false,
    bool Kar = false,
    bool Telefon = false,
    bool Adres = false,
    bool Notlar = false)
{
    /// <summary>Süper admin / kısıtsız bağlam.</summary>
    public static readonly AlanIzinleri Tam = new(true, true, true, true, true);

    /// <summary>Hiçbir hassas alanın görünmediği bağlam.</summary>
    public static readonly AlanIzinleri Yok = new();

    /// <summary>Maskelenmiş metin — "boş" ile "gizli" karışmasın diye ayırt edilebilir.</summary>
    public const string Gizli = "•••";

    /// <summary>Telefonu izne göre döndürür; yetki yoksa maske (boş değer boş kalır).</summary>
    public string? Telefonla(string? deger) => Telefon || string.IsNullOrEmpty(deger) ? deger : Gizli;

    /// <summary>Adresi izne göre döndürür; yetki yoksa maske.</summary>
    public string? Adresle(string? deger) => Adres || string.IsNullOrEmpty(deger) ? deger : Gizli;

    /// <summary>Personel/özel notu izne göre döndürür; yetki yoksa maske.</summary>
    public string? Notla(string? deger) => Notlar || string.IsNullOrEmpty(deger) ? deger : Gizli;

    /// <summary>Maliyet/kâr gibi SAYISAL alanlar maskelenemez → null döner.</summary>
    public decimal? Maliyetle(decimal? deger) => Maliyet ? deger : null;
    public decimal? Karla(decimal? deger) => Kar ? deger : null;

    /// <summary>Export kolonu bu alan iznine bağlıysa gösterilsin mi?</summary>
    public bool KolonGorunur(string? alanYetkisi) => alanYetkisi switch
    {
        null => true,
        "cost" => Maliyet,
        "margin" => Kar,
        "phone" => Telefon,
        "address" => Adres,
        "notes" => Notlar,
        _ => true,
    };
}
