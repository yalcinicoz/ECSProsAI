namespace ECSPros.Api.Models.Store;

/// <summary>
/// Ürün listesi sayfası (B7) görünüm modeli. Üç yüzeyi besler: kategori sayfası
/// (/{slug}), arama sonuçları (/urunler?search=) ve tüm ürünler (/urun-listesi).
/// İlk sayfa sunucudan render edilir (plan 3.3); devam sayfaları partial sonundaki
/// config script'i üzerinden api/store/* JSON'dan yüklenir.
/// </summary>
/// <summary>B8: renk tooltip satırı — eksen renginin kendi görseli ve detay linki.</summary>
public sealed record KartRenkVm(Guid ValueId, string Ad, string? GorselUrl);

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
    Guid? SeciliRenkId)                  // kategori kartlarında kartın rengi (detay linkine taşınır)
{
    public string Url => "/urun/" + Kod + (SeciliRenkId is { } renk ? "?color=" + renk : "");

    public string UrlRenkli(Guid renkValueId) => "/urun/" + Kod + "?color=" + renkValueId;

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
    string? KategorideArama = null)           // yalnız kategori sayfasında dolu olabilir
{
    /// <summary>SSR sonrası infinite scroll'un üreteceği kalan kart sayısı.</summary>
    public int KalanKart => Math.Max(0, ToplamUrun - IlkSayfa.Count);

    public string ToplamUrunMetni => ToplamUrun.ToString("N0", new System.Globalization.CultureInfo("tr-TR"));

    public bool DegerSecili(Guid valueId) => SeciliDegerler?.Contains(valueId) == true;
}
