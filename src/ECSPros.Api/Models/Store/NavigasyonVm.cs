namespace ECSPros.Api.Models.Store;

/// <summary>
/// Storefront navigasyonu (mega menü + mobil menü) için kategori ağacı.
/// Kaynak: kanal kategorileri (storefront.channel_categories) — nav_menus boş olduğu
/// sürece navigasyon doğrudan kategori ağacından beslenir (SPA ile aynı davranış).
/// </summary>
public sealed record NavKategori(
    Guid Id,
    string Ad,
    string Slug,
    string? GorselUrl,
    string? Rozet,
    IReadOnlyList<NavKategori> Cocuklar,
    bool UrunVar = true)
{
    public string Url => "/" + Slug;

    /// <summary>Torun kategori var mı? (mega/mobil menüde bölüm-başına-grid kurgusunu belirler)</summary>
    public bool UcuncuSeviyeVar => Cocuklar.Any(c => c.Cocuklar.Count > 0);

    /// <summary>Kendisinde veya herhangi bir alt dalında ürün var mı? (false = dalın tamamı boş)</summary>
    public bool DalindaUrunVar => UrunVar || Cocuklar.Any(c => c.DalindaUrunVar);
}

/// <param name="Kokler">Menülerde gösterilen ağaç — tamamı boş dallar budanmıştır.</param>
/// <param name="TumKokler">Budanmamış tam ağaç — doğrudan URL ile gelen boş kategori
/// sayfasının 404 yerine "henüz ürün yüklenmedi" mesajıyla açılabilmesi için.</param>
public sealed record NavigasyonVm(
    IReadOnlyList<NavKategori> Kokler,
    IReadOnlyList<NavKategori> TumKokler)
{
    public static readonly NavigasyonVm Bos = new([], []);
}
