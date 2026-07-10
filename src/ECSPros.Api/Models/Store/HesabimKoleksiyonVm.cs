namespace ECSPros.Api.Models.Store;

/// <summary>E6: Koleksiyonlarım sayfası SSR modeli — kartlar sunucuda render edilir;
/// kapak görselleri ürün kartı verisinden (silinen ürünler kapağa girmez).</summary>
public record HesabimKoleksiyonVm(
    Guid Id,
    string Ad,
    string? Aciklama,
    bool HerkeseAcik,
    bool Paylasilabilir,
    string ShareCode,
    string Status,               // pending | approved | rejected
    int ViewCount,
    bool KaydedilenlerMi,        // hızlı koleksiyon (bookmark hedefi)
    string SonGuncellemeMetni,
    int UrunSayisi,
    List<HesabimKoleksiyonUrunVm> KapakUrunleri); // ilk 3

public record HesabimKoleksiyonUrunVm(string Kod, string Ad, string? GorselUrl);
