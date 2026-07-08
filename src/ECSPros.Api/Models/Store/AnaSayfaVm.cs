namespace ECSPros.Api.Models.Store;

/// <summary>
/// Ana sayfa (B6) geçici kompozisyon görünüm modeli. Faz G kişiselleştirme sistemi
/// gelene kadar ana sayfa bu sabit kurgudan render edilir: kapsül kategori şeridi +
/// kök kategori başına bir standart ürün carousel'i (GorunumTipleri blokları birebir).
/// G8'de bu geçici kompozisyon kaldırılıp yerleşim tamamen vitrin sisteminden gelecek.
/// </summary>
public sealed record KapsulKategoriVm(string Ad, string Slug, string GorselUrl)
{
    public string Url => "/" + Slug;
}

public sealed record VitrinVm(string Baslik, string Slug, IReadOnlyList<UrunKartVm> Urunler)
{
    public string TumUrl => "/" + Slug;
}

public sealed record AnaSayfaVm(
    IReadOnlyList<KapsulKategoriVm> KapsulKategoriler,
    IReadOnlyList<VitrinVm> Vitrinler)
{
    public static readonly AnaSayfaVm Bos = new([], []);
}
