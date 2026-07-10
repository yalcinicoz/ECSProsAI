namespace ECSPros.Api.Models.Store;

/// <summary>E4: Siparişlerim sayfası SSR modeli — kartlar sunucuda render edilir
/// (misharix'in kart aç/kapat + filtre script'i parse anında dinleyici bağlar,
/// dinamik eklenen karta bağlayamaz). Detay modalı aynı verinin gömülü JSON'undan dolar.</summary>
public record HesabimSiparisVm(
    Guid Id,
    string OrderNumber,
    string TarihMetni,           // "1 Temmuz 2026"
    string Status,               // pending/confirmed/processing/shipped/delivered/cancelled/returned
    string DurumMetni,           // rozet yazısı
    string DurumSinifi,          // ms-hesabim-siparis-durum-* eki
    string FiltreAnahtari,       // devam | tamamlanan
    int AkisAdimi,               // 1-4 (cancelled: 1)
    decimal ToplamTutar,
    decimal AraToplam,
    decimal Indirim,
    string OdemeMetni,
    List<HesabimSiparisUrunVm> Urunler,
    HesabimKargoVm? Kargo,
    string TeslimatAdi,
    string TeslimatAdresi);

public record HesabimSiparisUrunVm(
    string Ad,
    string? SecenekOzeti,        // "Beden: S · Renk: Siyah"
    int Adet,
    decimal Fiyat,
    string? GorselUrl,
    string? UrunLink);

public record HesabimKargoVm(
    string? TakipNo,
    string? TakipUrl,
    string DurumMetni,
    string? TahminiTeslim,
    List<(string Baslik, string Detay, bool Aktif)> Adimlar);
