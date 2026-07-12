namespace ECSPros.Api.Models.Store;

/// <summary>H9: değerlendirmeler sayfası SSR modeli — sol ürün özeti (kart verisinden)
/// + başlık istatistiği + puan filtresi adetleri. Yorum listesi client fetch'iyle dolar
/// (infinite — sayfa script'i /api/store/reviews/product/{kod} çağırır).</summary>
public sealed record UrunDegerlendirmeleriVm(
    string Kod,
    string Ad,
    decimal Fiyat,
    decimal? EskiFiyat,
    string? GorselUrl,
    Guid FirmPlatformId,
    double Ortalama,
    int ToplamDegerlendirme,
    int YorumSayisi,
    IReadOnlyDictionary<int, int> PuanDagilimi); // 5..1 → adet
