using ECSPros.Iam.Application.Yetkilendirme;

namespace ECSPros.Api.Grid;

/// <summary>Yetki kataloğu Excel kolonları (teknik anahtar kilitli).</summary>
public static class YetkiKatalogExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<YetkiKatalogExportRow>> All = new GridExportColumn<YetkiKatalogExportRow>[]
    {
        new("code", "Anahtar", r => r.Code, Locked: true),
        new("ad", "Ad", r => r.Ad),
        new("aciklama", "Açıklama", r => r.Aciklama),
        new("modul", "Modül", r => r.Modul),
        new("tur", "Tür", r => YetkiKatalogGrid.TurEtiketi(r.Tur)),
        new("sayfa", "Sayfa Grubu", r => r.Sayfa),
        new("kanalKapsamli", "Kanal Bazlı", r => r.KanalKapsamli ? "Evet" : "Hayır"),
        new("grupSayisi", "Grup", r => r.GrupSayisi),
        new("kullaniciSayisi", "İstisna", r => r.KullaniciSayisi),
        new("koddaTanimli", "Uygulamada Var", r => r.KoddaTanimli ? "Evet" : "Hayır"),
        new("aktif", "Aktif", r => r.Aktif ? "Evet" : "Hayır"),
        new("sira", "Sıra", r => r.Sira),
    };
}
