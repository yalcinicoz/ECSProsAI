namespace ECSPros.Api.Models.Store;

/// <summary>
/// Ürün detay sayfası (B9) görünüm modeli. Sayfa tamamen SSR: seçili renk
/// (?color=valueId) sunucuda çözülür, renk değişimi yeni SSR isteğidir.
/// Beden seçimi client-side'dır (misharix'in kendi script'i); sepete ekleme
/// partial sonundaki config script'i üzerinden api/store/cart'a gider.
/// </summary>
public sealed record RenkSecenekVm(Guid ValueId, string Ad, string GorselUrl, bool Secili)
{
    public string Url => "?color=" + ValueId;
}

public sealed record BedenSecenekVm(string Ad, Guid VariantId, decimal Fiyat, bool Satilabilir = true); // B12: anahtar açıkken gerçek stoktan

public sealed record OzellikVm(string Ad, string Deger);

public sealed record BreadcrumbAdimVm(string Ad, string? Url);

public sealed record UrunDetayVm(
    string Kod,
    string Ad,
    decimal? Fiyat,
    decimal? EskiFiyat,                       // yalnız Fiyat'tan büyükse dolu (indirim)
    string? SeciliRenkAd,
    IReadOnlyList<string> Gorseller,          // seçili rengin galeri görselleri (tam URL)
    IReadOnlyList<RenkSecenekVm> Renkler,
    string BedenEtiketi,                      // varyant ekseninin adı ("Beden", "Numara"...)
    IReadOnlyList<BedenSecenekVm> Bedenler,
    Guid? TekVaryantId,                       // beden ekseni yokken sepete eklenecek varyant
    decimal? TekVaryantFiyat,
    IReadOnlyList<OzellikVm> Ozellikler,
    string? Aciklama,
    string? KisaAciklama,
    IReadOnlyList<BreadcrumbAdimVm> Breadcrumb,   // kategori zinciri (Anasayfa hariç)
    Guid FirmPlatformId,
    string ParaBirimi,
    double Puan = 0,                          // E7: onaylı yorum ortalaması
    int PuanSayisi = 0,                       // E7: onaylı yorum sayısı
    IReadOnlyList<YorumVm>? Yorumlar = null,  // E7: yayında ilk 10 yorum (SSR)
    IReadOnlyList<UrunVideoVm>? Videolar = null) // H5: galeri video slaytları (efektif URL)
{
    public int IndirimYuzdesi =>
        EskiFiyat is { } eski && Fiyat is { } yeni && eski > yeni
            ? (int)Math.Round((1 - yeni / eski) * 100)
            : 0;

    private static readonly System.Globalization.CultureInfo Tr = new("tr-TR");

    public static string FiyatMetni(decimal? fiyat) =>
        fiyat is { } f ? f.ToString("N2", Tr) + " TL" : "—";
}

/// <summary>E7: detay sayfası değerlendirme satırı.</summary>
public sealed record YorumVm(int Puan, string? Metin, string Ad, string TarihMetni);

public sealed record UrunVideoVm(string Url, string? ThumbnailUrl); // H5
