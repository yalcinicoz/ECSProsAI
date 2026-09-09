using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Iam.Infrastructure.Services;

/// <summary>
/// <see cref="IEtkinYetkiServisi"/> uygulaması (Y1).
///
/// Hesap: <c>(gruplar ∪ kullanıcı-ver) − kullanıcı-kaldır</c> — kural
/// <see cref="EfektifYetkiHesabi"/>'nda, burada yalnız veri toplanır.
/// Süper admin bayrağı varsa hesap yapılmaz (K5).
///
/// Önbellek: L1 <see cref="IMemoryCache"/> (kısa, düğüm-yerel) + L2 <see cref="ICacheService"/>
/// (Redis; erişilemezse sessizce atlanır — mevcut hata-güvenli sarmalayıcı). Yetki değişiminde
/// iki katman da düşürülür. Çoklu düğümde diğer düğümün L1'i en fazla <see cref="L1Sure"/> kadar
/// eski kalabilir; tek düğümde değişiklik anındadır.
/// </summary>
public class EtkinYetkiServisi(
    IIamDbContext db,
    IMemoryCache l1,
    ICacheService l2) : IEtkinYetkiServisi
{
    /// <summary>L1 ömrü kısadır: çoklu düğümde tazelik sınırı budur (tasarım §C.4).</summary>
    private static readonly TimeSpan L1Sure = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan L2Sure = TimeSpan.FromMinutes(5);

    private static string L1Anahtar(Guid userId) => $"yetki:eff:{userId:N}";
    private static string L2Anahtar(Guid userId) => $"iam:yetki:{userId:N}";

    public async Task<EfektifYetkiler> GetirAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty) return EfektifYetkiler.Bos;

        if (l1.TryGetValue(L1Anahtar(userId), out EfektifYetkiler? hazir) && hazir is not null)
            return hazir;

        var paket = await l2.GetAsync<YetkiPaketi>(L2Anahtar(userId), ct);
        if (paket is null)
        {
            paket = await HesaplaAsync(userId, ct);
            await l2.SetAsync(L2Anahtar(userId), paket, L2Sure, ct);
        }

        var sonuc = paket.Coz();
        l1.Set(L1Anahtar(userId), sonuc, L1Sure);
        return sonuc;
    }

    public async Task GecersizKilAsync(Guid userId, CancellationToken ct = default)
    {
        l1.Remove(L1Anahtar(userId));
        await l2.RemoveAsync(L2Anahtar(userId), ct);
    }

    public async Task GrupIcinGecersizKilAsync(Guid roleId, CancellationToken ct = default)
    {
        var uyeler = await db.UserRoles.AsNoTracking()
            .Where(ur => ur.RoleId == roleId && !ur.IsDeleted)
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var uye in uyeler)
            await GecersizKilAsync(uye, ct);
    }

    private async Task<YetkiPaketi> HesaplaAsync(Guid userId, CancellationToken ct)
    {
        var kullanici = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => new { u.IsSuperAdmin })
            .FirstOrDefaultAsync(ct);

        if (kullanici is null) return new YetkiPaketi(false, []);   // pasif/silinmiş → yetki yok
        if (kullanici.IsSuperAdmin) return new YetkiPaketi(true, []);

        // Gruplardan (rol) gelenler
        var gruptan = await (
            from ur in db.UserRoles.AsNoTracking().Where(x => x.UserId == userId && !x.IsDeleted)
            join rp in db.RolePermissions.AsNoTracking().Where(x => !x.IsDeleted) on ur.RoleId equals rp.RoleId
            join p in db.Permissions.AsNoTracking().Where(x => x.IsActive && !x.IsDeleted) on rp.PermissionId equals p.Id
            select new { p.Code, p.ChannelScoped, rp.ChannelIds })
            .ToListAsync(ct);

        // Kullanıcı istisnaları (ver / kaldır)
        var istisnalar = await (
            from up in db.UserPermissions.AsNoTracking().Where(x => x.UserId == userId && !x.IsDeleted)
            join p in db.Permissions.AsNoTracking().Where(x => x.IsActive && !x.IsDeleted) on up.PermissionId equals p.Id
            select new { p.Code, p.ChannelScoped, up.ChannelIds, up.GrantType })
            .ToListAsync(ct);

        var kaynaklar = new List<YetkiKaynagi>(gruptan.Count + istisnalar.Count);
        foreach (var g in gruptan)
            kaynaklar.Add(new YetkiKaynagi(g.Code, g.ChannelScoped, g.ChannelIds, YetkiKaynakTipi.Grup));
        foreach (var i in istisnalar)
            kaynaklar.Add(new YetkiKaynagi(i.Code, i.ChannelScoped, i.ChannelIds,
                i.GrantType == "revoke" ? YetkiKaynakTipi.KullaniciKaldir : YetkiKaynakTipi.KullaniciVer));

        var efektif = EfektifYetkiHesabi.Hesapla(false, kaynaklar);
        return YetkiPaketi.Kur(efektif);
    }

    /// <summary>L2'de saklanan sadeleştirilmiş biçim (JSON serileştirilebilir).</summary>
    public sealed record YetkiPaketi(bool SuperAdmin, Dictionary<string, List<Guid>?> Yetkiler)
    {
        public static YetkiPaketi Kur(EfektifYetkiler e)
        {
            var d = new Dictionary<string, List<Guid>?>(StringComparer.Ordinal);
            foreach (var key in e.Keyler)
                d[key] = e.Kanallar(key)?.ToList();
            return new YetkiPaketi(e.SuperAdmin, d);
        }

        public EfektifYetkiler Coz()
        {
            if (SuperAdmin) return new EfektifYetkiler(true, new Dictionary<string, HashSet<Guid>?>());
            var d = new Dictionary<string, HashSet<Guid>?>(StringComparer.Ordinal);
            foreach (var (k, v) in Yetkiler)
                d[k] = v is null ? null : new HashSet<Guid>(v);
            return new EfektifYetkiler(false, d);
        }
    }
}
