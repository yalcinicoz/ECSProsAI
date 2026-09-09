using ECSPros.Iam.Domain.Entities;
using ECSPros.Shared.Kernel.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Services;

/// <summary>
/// Kod sahipli permission kataloğunun DB ile eşitlenmesi (Y0, karar K4).
///
/// Kurallar (tasarım §F.1, §M.2, §M.4):
///  • Katalogda olup DB'de olmayan → EKLENİR (aktif, ama KİMSEYE atanmaz — default deny).
///  • Katalogda ve DB'de olan → yalnız KOD SAHİPLİ alanlar güncellenir
///    (Kind, ChannelScoped, Module/PageCode varsayılanı, IsCodeDefined).
///    Görünen ad/açıklama/sıra PANELE aittir; ilk eklemeden sonra ASLA ezilmez.
///  • DB'de olup katalogda olmayan → IsCodeDefined=false + IsActive=false ("uygulamada
///    karşılığı yok"). Kayıt SİLİNMEZ; atamalar ve audit geçmişi korunur.
///
/// Panelin kanal-kapsamlı bayrağını değiştirebilmesi (K4) ile kodun bu alanı sahiplenmesi
/// çelişir; çözüm: katalog değeri yazılır ta ki PANEL değiştirene kadar
/// (<c>Permission.ChannelScopedOverridden</c> açık bayrağı) — tarih/heuristik tahmini yok.
/// </summary>
public static class PermissionKatalogSenkronu
{
    public sealed record Sonuc(int Eklenen, int Guncellenen, int Pasiflenen);

    public static async Task<Sonuc> CalistirAsync(IIamDbContext db, CancellationToken ct = default)
    {
        var katalog = PermissionKatalogu.Tumu;
        var mevcutlar = await db.Permissions.ToListAsync(ct);
        var mevcutIndeks = mevcutlar.ToDictionary(p => p.Code, StringComparer.Ordinal);

        int eklenen = 0, guncellenen = 0, pasiflenen = 0;

        foreach (var t in katalog)
        {
            if (!mevcutIndeks.TryGetValue(t.Key, out var kayit))
            {
                db.Permissions.Add(new Permission
                {
                    Code = t.Key,
                    NameI18n = new Dictionary<string, string> { ["tr"] = t.Ad },
                    DescriptionI18n = t.Aciklama is null
                        ? null : new Dictionary<string, string> { ["tr"] = t.Aciklama },
                    Module = t.Modul,
                    PageCode = t.Sayfa,
                    Kind = KindKodu(t.Tur),
                    ChannelScoped = t.KanalKapsamli,   // yalnız ilk eklemede — sonrası panelin
                    PermissionType = "manage",          // eski alan; geriye dönük uyum
                    SortOrder = t.Sira,
                    IsCodeDefined = true,
                    IsActive = true,
                });
                eklenen++;
                continue;
            }

            var degisti = false;
            var kind = KindKodu(t.Tur);
            if (kayit.Kind != kind) { kayit.Kind = kind; degisti = true; }
            if (kayit.Module != t.Modul) { kayit.Module = t.Modul; degisti = true; }
            if (!kayit.IsCodeDefined) { kayit.IsCodeDefined = true; degisti = true; }
            // Kanal-kapsamlı bayrağı: panel değiştirmediyse KOD değeri geçerlidir.
            if (!kayit.ChannelScopedOverridden && kayit.ChannelScoped != t.KanalKapsamli)
            { kayit.ChannelScoped = t.KanalKapsamli; degisti = true; }
            // Sayfa grubu panelde boş bırakılmışsa katalog varsayılanı doldurulur.
            if (string.IsNullOrWhiteSpace(kayit.PageCode) && t.Sayfa is not null)
            { kayit.PageCode = t.Sayfa; degisti = true; }
            // Görünen ad hiç yazılmamışsa (eski seed) katalogdan doldurulur.
            if (kayit.NameI18n is null || kayit.NameI18n.Count == 0)
            { kayit.NameI18n = new Dictionary<string, string> { ["tr"] = t.Ad }; degisti = true; }

            if (degisti)
            {
                kayit.UpdatedAt = DateTime.UtcNow;
                guncellenen++;
            }
        }

        var katalogKeyleri = katalog.Select(t => t.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var kayit in mevcutlar.Where(p => !katalogKeyleri.Contains(p.Code)))
        {
            if (!kayit.IsCodeDefined && !kayit.IsActive) continue;   // zaten işaretli
            kayit.IsCodeDefined = false;
            kayit.IsActive = false;                                   // yeni atama yapılamaz
            kayit.UpdatedAt = DateTime.UtcNow;
            pasiflenen++;
        }

        if (eklenen + guncellenen + pasiflenen > 0)
            await db.SaveChangesAsync(ct);

        return new Sonuc(eklenen, guncellenen, pasiflenen);
    }

    public static string KindKodu(PermissionTuru tur) => tur switch
    {
        PermissionTuru.Sayfa => "page",
        PermissionTuru.Alan => "field",
        _ => "action",
    };
}
