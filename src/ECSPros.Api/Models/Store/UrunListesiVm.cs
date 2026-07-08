namespace ECSPros.Api.Models.Store;

/// <summary>
/// Ürün listesi sayfası (B7) görünüm modeli. Üç yüzeyi besler: kategori sayfası
/// (/{slug}), arama sonuçları (/urunler?search=) ve tüm ürünler (/urun-listesi).
/// İlk sayfa sunucudan render edilir (plan 3.3); devam sayfaları partial sonundaki
/// config script'i üzerinden api/store/* JSON'dan yüklenir.
/// </summary>
public sealed record UrunKartVm(
    string Kod,
    string Ad,
    string? GorselUrl,
    decimal Fiyat,
    IReadOnlyList<string> RenkHexleri,   // rozet için ilk 2 + sayaç (B8: tooltip)
    int RenkSayisi,
    IReadOnlyList<Guid> DegerIdler)      // client-side filtre eşleşmesi (SPA paritesi)
{
    public string Url => "/urun/" + Kod;
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
    IReadOnlyList<NavKategori> KategoriSecenekleri)
{
    /// <summary>SSR sonrası infinite scroll'un üreteceği kalan kart sayısı.</summary>
    public int KalanKart => Math.Max(0, ToplamUrun - IlkSayfa.Count);

    public string ToplamUrunMetni => ToplamUrun.ToString("N0", new System.Globalization.CultureInfo("tr-TR"));
}
