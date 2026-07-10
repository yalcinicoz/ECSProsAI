namespace ECSPros.Api.Models.Store;

/// <summary>E9: İndirim Kuponlarım kartı — kupon adı/kodu + indirim ve koşul metinleri
/// sunucuda hazırlanır; "Sepette Kullan" C3'ün sessionStorage kupon sözleşmesiyle sepete taşır.</summary>
public record HesabimKuponVm(
    string Code,
    string IndirimMetni,    // "%10 indirim" / "150,00 TL indirim"
    string KosulMetni);     // "750,00 TL ve üzeri alışverişlerde geçerli. Son kullanım: 15.07.2026"

/// <summary>E10: Tekrar Satın Al kartı — üyenin teslim edilmiş sipariş kalemlerinden
/// türetilir (varyant başına bir kart, son alışveriş tarihiyle); fiyat GÜNCEL satış
/// fiyatıdır (PlatformPrice ?? BasePrice — sipariş anındaki değil), Sepete Ekle
/// C1 sepet API'sine gider.</summary>
public record HesabimTekrarUrunVm(
    Guid VariantId,
    string Ad,
    string? SecenekOzeti,       // "Beden: M"
    string SonAlisverisMetni,   // "28.06.2026"
    decimal Fiyat,
    string? GorselUrl,
    string? UrunLink);
