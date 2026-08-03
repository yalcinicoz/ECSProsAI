namespace ECSPros.Api.Models.Store;

/// <summary>
/// Ürün listesi sayfası (B7) görünüm modeli. Üç yüzeyi besler: kategori sayfası
/// (/{slug}), arama sonuçları (/urunler?search=) ve tüm ürünler (/urun-listesi).
/// İlk sayfa sunucudan render edilir (plan 3.3); devam sayfaları partial sonundaki
/// config script'i üzerinden api/store/* JSON'dan yüklenir.
/// </summary>
/// <summary>B8: renk tooltip satırı — eksen renginin kendi görseli ve detay linki.</summary>
// Slug: rengin gerçek (legacy) URL slug'ı — varsa detay linki doğrudan /{slug}, yoksa
// /urun/{kod}?color= (301 güvenlik ağı). URL aktarımı 2b.
public sealed record KartRenkVm(Guid ValueId, string Ad, string? GorselUrl, string? Slug = null);

public sealed record UrunKartVm(
    string Kod,
    string Ad,
    string? GorselUrl,
    decimal Fiyat,
    IReadOnlyList<string> RenkHexleri,   // rozet için ilk 2 + sayaç
    int RenkSayisi,
    IReadOnlyList<Guid> DegerIdler,      // client-side filtre eşleşmesi (SPA paritesi)
    IReadOnlyList<string> GaleriUrller,  // B8: hover galerisi (seçili rengin ilk 4 görseli)
    IReadOnlyList<KartRenkVm> RenkSecenekleri, // B8: renk tooltip'i
    Guid? SeciliRenkId,                  // kategori kartlarında kartın rengi (detay linkine taşınır)
    bool Sponsorlu = false,              // B11: öne çıkar penceresi içinde — "Sponsorlu" rozeti
    double Puan = 0,                     // E7: onaylı yorum ortalaması (0 = yorum yok)
    int PuanSayisi = 0,                  // E7: onaylı yorum sayısı
    string? VideoUrl = null,             // H5: Videolu Ürün rozeti + hover tooltip videosu
    string? Slug = null,                 // URL aktarımı 2b: kartın (seçili renk) gerçek URL slug'ı
    decimal? EskiFiyat = null,           // 2026-07-31: indirim öncesi (çizili) fiyat — CompareAtPrice > Fiyat ise
    string? KampanyaAdi = null,          // F3: kampanya adı/rozeti (varsa gösterilir)
    decimal? KampanyaFiyat = null,       // F3: ürün-bazlı kampanyalı fiyat (null = sepette/yok)
    IReadOnlyList<string>? KampanyaAdlari = null) // 2026-08-03: ürünü kapsayan TÜM kampanyalar — bantta dönüşümlü
{
    // Bantta gösterilecek kampanya listesi (çoklu yoksa tekile düşer).
    public IReadOnlyList<string> KampanyaBantListesi => KampanyaAdlari is { Count: > 0 } liste
        ? liste
        : (KampanyaAdi is { Length: > 0 } tek ? new[] { tek } : System.Array.Empty<string>());

    // İndirim yüzdesi (CompareAtPrice > Fiyat ise) — tam sayı, rozet için ("-%23").
    public int? IndirimYuzdesi => EskiFiyat is { } e && e > Fiyat && e > 0
        ? (int)System.Math.Round((e - Fiyat) / e * 100m, System.MidpointRounding.AwayFromZero)
        : null;

    // F3: kampanyalı fiyat gerçekten satış fiyatının altında mı (gösterilecek mi).
    public bool KampanyaFiyatVar => KampanyaFiyat is { } kf && kf > 0 && kf < Fiyat;
    // F3: kampanya var ama ürün-bazlı fiyat yok = sepette geçerli tip.
    public bool KampanyaSepette => KampanyaAdi is { Length: > 0 } && !KampanyaFiyatVar;

    // 2b: slug varsa doğrudan gerçek URL (301 hop'u yok); yoksa /urun/{kod}?color= (güvenlik ağı).
    public string Url => Slug is { Length: > 0 } s
        ? "/" + s
        : "/urun/" + Kod + (SeciliRenkId is { } renk ? "?color=" + renk : "");

    public string UrlRenkli(Guid renkValueId) =>
        RenkSecenekleri.FirstOrDefault(r => r.ValueId == renkValueId)?.Slug is { Length: > 0 } rs
            ? "/" + rs
            : "/urun/" + Kod + "?color=" + renkValueId;

    public string GaleriResimleri =>
        GaleriUrller.Count > 0 ? string.Join("|", GaleriUrller) : GorselUrl ?? "";
}

public sealed record FiltreDegerVm(Guid ValueId, string Ad, string? Hex, int UrunSayisi);

public sealed record FiltreGrupVm(
    string TipKodu,
    string Ad,
    bool RenkTipi,
    IReadOnlyList<FiltreDegerVm> Degerler);

public sealed record UrunListesiVm(
    string Baslik,
    int ToplamUrun,
    int SayfaBoyu,
    IReadOnlyList<UrunKartVm> IlkSayfa,
    string DevamApiUrl,                       // "&page=N" eklenerek çağrılır
    IReadOnlyList<FiltreGrupVm> FiltreGruplari,
    decimal FiyatMin,
    decimal FiyatMax,
    IReadOnlyList<NavKategori> KategoriSecenekleri,
    // B10: sunucu tarafı filtre/sıralama durumu — SSR checkbox/select ön-seçimi ve
    // URL yeniden kurma (client script navigasyonla uygular) için.
    IReadOnlyList<Guid>? SeciliDegerler = null,
    decimal? SeciliFiyatMin = null,
    decimal? SeciliFiyatMax = null,
    string? SeciliSiralama = null,
    string? KategorideArama = null,           // yalnız kategori sayfasında dolu olabilir
    // 2026-07-17: paylaşılan ?page=N ile gelindiğinde SSR bu sayfadan başlar; infinite
    // scroll devamı bu ofsetle ister, URL kaydırmayla replaceState ile güncellenir.
    int BaslangicSayfa = 1,
    // 2026-07-18: liste boşken gösterilecek açıklama (boş kategori / sonuçsuz arama /
    // sonuçsuz filtre) — null ise liste doludur, blok hiç basılmaz.
    string? BosDurumMesaji = null)
{
    /// <summary>SSR sonrası infinite scroll'un üreteceği kalan kart sayısı.</summary>
    public int KalanKart => Math.Max(0, ToplamUrun - IlkSayfa.Count);

    public string ToplamUrunMetni => ToplamUrun.ToString("N0", new System.Globalization.CultureInfo("tr-TR"));

    public bool DegerSecili(Guid valueId) => SeciliDegerler?.Contains(valueId) == true;
}
