namespace ECSPros.Api.Models.Store;

/// <summary>E8: İadelerim sayfası SSR modeli — iade kartları sunucuda render edilir
/// (E4 Siparişlerim deseni). Yeni İade Talebi modalının iade edilebilir ürünleri ve
/// neden listesi de SSR verisinden dolar.</summary>
public record HesabimIadeVm(
    Guid Id,
    string ReturnNumber,
    string TarihMetni,               // "29 Mayıs 2026"
    string Status,                   // requested/approved/received/refunded/rejected
    string DurumMetni,               // rozet yazısı
    string DurumSinifi,              // ms-hesabim-siparis-durum-* eki
    string FiltreAnahtari,           // devam | tamamlanan
    int AkisAdimi,                   // 1-4: tamamlanan adım sayısı (rejected: akış gizli)
    List<HesabimSiparisUrunVm> Urunler,
    string? KargoIadeKodu,
    string BilgiBaslik,
    string BilgiMetin,
    bool BilgiUyari,                 // true → ms-hesabim-iade-bilgi-uyari
    decimal BeklenenTutar,
    bool Tamamlandi);                // refunded → "İade Tutarı", değilse "Beklenen İade Tutarı"

/// <summary>Yeni İade Talebi modalındaki iade edilebilir (veya edilmiş) sipariş kalemi.</summary>
public record HesabimIadeEdilebilirUrunVm(
    Guid OrderItemId,
    string SiparisNo,
    string SiparisTarihi,
    decimal SiparisTutari,
    string Ad,
    string? SecenekOzeti,
    int Adet,
    decimal Tutar,
    string? GorselUrl,
    bool IadeEdildi,
    List<HesabimIadeNedenSecimVm> OncekiNedenler);

/// <summary>ReturnItem.CustomerNotes JSON'undan çözülen neden seçimi (ana + altlar).</summary>
public record HesabimIadeNedenSecimVm(string Ana, List<string> Altlar);

/// <summary>Lookup'tan gelen ana iade nedeni + alt neden listesi (ExtraData.subReasons).</summary>
public record HesabimIadeNedeniVm(Guid Id, string Ad, List<string> AltNedenler);
