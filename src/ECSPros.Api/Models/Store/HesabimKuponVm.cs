namespace ECSPros.Api.Models.Store;

/// <summary>E9: İndirim Kuponlarım kartı — kupon adı/kodu + indirim ve koşul metinleri
/// sunucuda hazırlanır; "Sepette Kullan" C3'ün sessionStorage kupon sözleşmesiyle sepete taşır.</summary>
public record HesabimKuponVm(
    string Code,
    string IndirimMetni,    // "%10 indirim" / "150,00 TL indirim"
    string KosulMetni);     // "750,00 TL ve üzeri alışverişlerde geçerli. Son kullanım: 15.07.2026"
