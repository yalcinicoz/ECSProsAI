using ECSPros.Iam.Application.Yetkilendirme;

namespace ECSPros.Api.Grid;

/// <summary>Yetki grupları Excel kolonları (grup kodu kilitli — denetim izi ona bağlı).</summary>
public static class YetkiGrubuExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<YetkiGrubuExportRow>> All = new GridExportColumn<YetkiGrubuExportRow>[]
    {
        new("ad", "Grup", r => r.Ad, Locked: true),
        new("code", "Kod", r => r.Code),
        new("aciklama", "Açıklama", r => r.Aciklama),
        new("kullaniciSayisi", "Kullanıcı", r => r.KullaniciSayisi),
        new("yetkiSayisi", "Yetki", r => r.YetkiSayisi),
        new("sistem", "Sistem Grubu", r => r.Sistem ? "Evet" : "Hayır"),
        new("gecisGrubu", "Geçici Grup", r => r.GecisGrubu ? "Evet" : "Hayır"),
        new("aktif", "Aktif", r => r.Aktif ? "Evet" : "Hayır"),
    };
}
