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
    IReadOnlyList<NavKategori> Cocuklar)
{
    public string Url => "/" + Slug;

    /// <summary>Torun kategori var mı? (mega/mobil menüde bölüm-başına-grid kurgusunu belirler)</summary>
    public bool UcuncuSeviyeVar => Cocuklar.Any(c => c.Cocuklar.Count > 0);
}

public sealed record NavigasyonVm(IReadOnlyList<NavKategori> Kokler)
{
    public static readonly NavigasyonVm Bos = new([]);
}
