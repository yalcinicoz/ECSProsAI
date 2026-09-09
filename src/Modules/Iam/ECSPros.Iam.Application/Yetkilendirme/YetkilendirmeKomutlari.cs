using ECSPros.Iam.Application.Services;
using ECSPros.Iam.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Yetkilendirme;

// Y4 (2026-09-09): yetkilendirme panelinin yazma uçları.
//
// ESKALASYON KORUMASI (tasarım §O.4): yetkilendirme yapan kullanıcı, KENDİ sahip olmadığı bir
// yetkiyi ya da kanalı başkasına veremez — aksi hâlde kendine grup açıp dolaylı olarak yükselir.
// Süper admin bu kısıttan muaftır. Denetim komutların içinde, tek yerde yapılır.

/// <summary>İşlemi yapan kullanıcının kendi kapsamı (eskalasyon denetimi için).</summary>
public record IsleyenKapsami(bool SuperAdmin, IReadOnlyDictionary<string, IReadOnlyCollection<Guid>?> Yetkiler)
{
    public bool Verebilir(string code, IReadOnlyCollection<Guid>? kanallar)
    {
        if (SuperAdmin) return true;
        if (!Yetkiler.TryGetValue(code, out var kendi)) return false;
        if (kendi is null) return true;                       // kapsamsız yetki: sahibiyse verebilir
        if (kanallar is null || kanallar.Count == 0) return true;
        return kanallar.All(k => kendi.Contains(k));
    }
}

public record GrupYetkiGirdisi(Guid PermissionId, List<Guid>? ChannelIds);

// ── Grup oluştur / güncelle ────────────────────────────────────────────────────
public record YetkiGrubuKaydetCommand(Guid? Id, string Ad, string? Aciklama, bool Aktif, Guid IsleyenId)
    : IRequest<Result<Guid>>;

public class YetkiGrubuKaydetCommandHandler(IIamDbContext db, IEtkinYetkiServisi yetkiServisi, IYetkiDenetimi denetim)
    : IRequestHandler<YetkiGrubuKaydetCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(YetkiGrubuKaydetCommand r, CancellationToken ct)
    {
        var ad = r.Ad?.Trim() ?? "";
        if (ad.Length < 2) return Result.Failure<Guid>("Grup adı en az 2 karakter olmalıdır.");

        Role grup;
        if (r.Id is { } id)
        {
            var mevcut = await db.Roles.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (mevcut is null) return Result.Failure<Guid>("Yetki grubu bulunamadı.");
            grup = mevcut;
            grup.UpdatedAt = DateTime.UtcNow;
            grup.UpdatedBy = r.IsleyenId;
        }
        else
        {
            var kod = Kodla(ad);
            if (await db.Roles.AnyAsync(x => x.Code == kod, ct))
                return Result.Failure<Guid>($"'{kod}' kodlu bir grup zaten var; farklı bir ad seçin.");
            grup = new Role { Code = kod, IsSystem = false, CreatedBy = r.IsleyenId };
            db.Roles.Add(grup);
        }

        grup.NameI18n = new Dictionary<string, string> { ["tr"] = ad };
        grup.DescriptionI18n = string.IsNullOrWhiteSpace(r.Aciklama)
            ? null : new Dictionary<string, string> { ["tr"] = r.Aciklama!.Trim() };
        grup.IsActive = r.Aktif;

        var yeniMi = r.Id is null;
        await db.SaveChangesAsync(ct);
        if (!yeniMi) await yetkiServisi.GrupIcinGecersizKilAsync(grup.Id, ct);

        await denetim.YazAsync(new YetkiDenetimKaydi(
            yeniMi ? "yetki.grup.olustur" : "yetki.grup.guncelle",
            yeniMi ? $"\"{ad}\" yetki grubunu oluşturdu."
                   : $"\"{ad}\" yetki grubunu güncelledi (durum: {(r.Aktif ? "aktif" : "pasif")}).",
            HedefGrupId: grup.Id), ct);

        return Result.Success(grup.Id);
    }

    private static string Kodla(string ad)
    {
        var s = ad.Trim().ToLowerInvariant()
            .Replace('ı', 'i').Replace('ğ', 'g').Replace('ü', 'u')
            .Replace('ş', 's').Replace('ö', 'o').Replace('ç', 'c');
        var temiz = new string(s.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        while (temiz.Contains("__")) temiz = temiz.Replace("__", "_");
        return temiz.Trim('_');
    }
}

// ── Grubun yetkilerini kaydet (topluca değiştirir) ─────────────────────────────
public record GrupYetkileriKaydetCommand(Guid GroupId, List<GrupYetkiGirdisi> Yetkiler,
    IsleyenKapsami Isleyen, Guid IsleyenId) : IRequest<Result<bool>>;

public class GrupYetkileriKaydetCommandHandler(IIamDbContext db, IEtkinYetkiServisi yetkiServisi, IYetkiDenetimi denetim)
    : IRequestHandler<GrupYetkileriKaydetCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(GrupYetkileriKaydetCommand r, CancellationToken ct)
    {
        var grup = await db.Roles.FirstOrDefaultAsync(x => x.Id == r.GroupId, ct);
        if (grup is null) return Result.Failure<bool>("Yetki grubu bulunamadı.");

        var katalog = await db.Permissions.AsNoTracking()
            .Where(p => p.IsActive)
            .Select(p => new { p.Id, p.Code, p.ChannelScoped })
            .ToListAsync(ct);
        var indeks = katalog.ToDictionary(p => p.Id);

        // Eskalasyon denetimi + geçersiz/pasif yetki reddi
        foreach (var g in r.Yetkiler)
        {
            if (!indeks.TryGetValue(g.PermissionId, out var p))
                return Result.Failure<bool>("Pasif ya da tanımsız bir yetki seçildi.");
            if (!r.Isleyen.Verebilir(p.Code, g.ChannelIds))
                return Result.Failure<bool>(
                    $"'{p.Code}' yetkisini (ya da seçtiğiniz kanalları) kendiniz taşımadığınız için veremezsiniz.");
        }

        var mevcut = await db.RolePermissions.Where(rp => rp.RoleId == r.GroupId).ToListAsync(ct);
        // Denetim için "önce" durumu (yetki kodu + kanal sayısı)
        var oncesi = mevcut.Where(m => !m.IsDeleted)
            .Select(m => indeks.TryGetValue(m.PermissionId, out var p0)
                ? $"{p0.Code}({m.ChannelIds?.Count ?? 0})" : m.PermissionId.ToString()[..8])
            .OrderBy(x => x).ToList();
        var istenen = r.Yetkiler.ToDictionary(x => x.PermissionId);

        foreach (var m in mevcut)
        {
            if (istenen.TryGetValue(m.PermissionId, out var g))
            {
                var kanalKapsamli = indeks[m.PermissionId].ChannelScoped;
                m.ChannelIds = kanalKapsamli ? (g.ChannelIds ?? new List<Guid>()) : null;
                m.IsDeleted = false; m.DeletedAt = null; m.DeletedBy = null;
                m.UpdatedAt = DateTime.UtcNow; m.UpdatedBy = r.IsleyenId;
                istenen.Remove(m.PermissionId);
            }
            else if (!m.IsDeleted)
            {
                m.IsDeleted = true; m.DeletedAt = DateTime.UtcNow; m.DeletedBy = r.IsleyenId;
            }
        }

        foreach (var (permissionId, g) in istenen)
            db.RolePermissions.Add(new RolePermission
            {
                RoleId = r.GroupId,
                PermissionId = permissionId,
                ChannelIds = indeks[permissionId].ChannelScoped ? (g.ChannelIds ?? new List<Guid>()) : null,
                CreatedBy = r.IsleyenId,
            });

        await db.SaveChangesAsync(ct);
        await yetkiServisi.GrupIcinGecersizKilAsync(r.GroupId, ct);   // K3: anında etkili

        var sonrasi = r.Yetkiler
            .Select(y => indeks.TryGetValue(y.PermissionId, out var p1)
                ? $"{p1.Code}({y.ChannelIds?.Count ?? 0})" : y.PermissionId.ToString()[..8])
            .OrderBy(x => x).ToList();
        var eklenen = sonrasi.Except(oncesi).ToList();
        var cikan = oncesi.Except(sonrasi).ToList();
        var grupAd = grup.NameI18n.TryGetValue("tr", out var gad) ? gad : grup.Code;
        await denetim.YazAsync(new YetkiDenetimKaydi(
            "yetki.grup.yetkiler",
            $"\"{grupAd}\" grubunun yetkilerini güncelledi " +
            $"({sonrasi.Count} yetki; +{eklenen.Count} eklendi, -{cikan.Count} çıkarıldı).",
            HedefGrupId: r.GroupId,
            Oncesi: string.Join(", ", oncesi),
            Sonrasi: string.Join(", ", sonrasi)), ct);

        return Result.Success(true);
    }
}

// ── Grup üyeliği ──────────────────────────────────────────────────────────────
public record GrupUyeligiDegistirCommand(Guid GroupId, Guid UserId, bool Ekle, Guid IsleyenId)
    : IRequest<Result<bool>>;

public class GrupUyeligiDegistirCommandHandler(IIamDbContext db, IEtkinYetkiServisi yetkiServisi, IYetkiDenetimi denetim)
    : IRequestHandler<GrupUyeligiDegistirCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(GrupUyeligiDegistirCommand r, CancellationToken ct)
    {
        if (!await db.Roles.AnyAsync(x => x.Id == r.GroupId, ct))
            return Result.Failure<bool>("Yetki grubu bulunamadı.");
        if (!await db.Users.AnyAsync(u => u.Id == r.UserId, ct))
            return Result.Failure<bool>("Kullanıcı bulunamadı.");

        var mevcut = await db.UserRoles.FirstOrDefaultAsync(x => x.UserId == r.UserId && x.RoleId == r.GroupId, ct);
        if (r.Ekle)
        {
            if (mevcut is null)
                db.UserRoles.Add(new UserRole { UserId = r.UserId, RoleId = r.GroupId, CreatedBy = r.IsleyenId });
            else if (mevcut.IsDeleted)
            { mevcut.IsDeleted = false; mevcut.DeletedAt = null; mevcut.DeletedBy = null; }
        }
        else if (mevcut is not null && !mevcut.IsDeleted)
        {
            mevcut.IsDeleted = true; mevcut.DeletedAt = DateTime.UtcNow; mevcut.DeletedBy = r.IsleyenId;
        }

        await db.SaveChangesAsync(ct);
        await yetkiServisi.GecersizKilAsync(r.UserId, ct);

        var grupAdi = await db.Roles.Where(x => x.Id == r.GroupId)
            .Select(x => x.NameI18n.ContainsKey("tr") ? x.NameI18n["tr"] : x.Code).FirstOrDefaultAsync(ct) ?? "";
        var kullaniciAdi = await db.Users.Where(u => u.Id == r.UserId)
            .Select(u => (u.FirstName + " " + u.LastName).Trim()).FirstOrDefaultAsync(ct) ?? "";
        await denetim.YazAsync(new YetkiDenetimKaydi(
            r.Ekle ? "yetki.grup.uye.ekle" : "yetki.grup.uye.cikar",
            r.Ekle ? $"{kullaniciAdi} kullanıcısını \"{grupAdi}\" grubuna ekledi."
                   : $"{kullaniciAdi} kullanıcısını \"{grupAdi}\" grubundan çıkardı.",
            HedefKullaniciId: r.UserId, HedefGrupId: r.GroupId), ct);

        return Result.Success(true);
    }
}

// ── Kullanıcı istisnası (ver / kaldır / temizle) ───────────────────────────────
/// <param name="Mod">ver | kaldir | yok (yok = istisnayı sil, gruplardan gelen kalır)</param>
public record KullaniciIstisnasiKaydetCommand(Guid UserId, Guid PermissionId, string Mod,
    List<Guid>? ChannelIds, IsleyenKapsami Isleyen, Guid IsleyenId) : IRequest<Result<bool>>;

public class KullaniciIstisnasiKaydetCommandHandler(IIamDbContext db, IEtkinYetkiServisi yetkiServisi, IYetkiDenetimi denetim)
    : IRequestHandler<KullaniciIstisnasiKaydetCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(KullaniciIstisnasiKaydetCommand r, CancellationToken ct)
    {
        if (r.Mod is not ("ver" or "kaldir" or "yok"))
            return Result.Failure<bool>("Geçersiz istisna modu.");

        var perm = await db.Permissions.AsNoTracking().FirstOrDefaultAsync(p => p.Id == r.PermissionId, ct);
        if (perm is null || !perm.IsActive) return Result.Failure<bool>("Pasif ya da tanımsız yetki.");
        if (!await db.Users.AnyAsync(u => u.Id == r.UserId, ct))
            return Result.Failure<bool>("Kullanıcı bulunamadı.");

        // Eskalasyon: "ver" yalnız kendi taşıdığın yetki/kanalla yapılabilir. "kaldir" daraltmadır, serbest.
        if (r.Mod == "ver" && !r.Isleyen.Verebilir(perm.Code, r.ChannelIds))
            return Result.Failure<bool>(
                $"'{perm.Code}' yetkisini (ya da seçtiğiniz kanalları) kendiniz taşımadığınız için veremezsiniz.");

        var mevcut = await db.UserPermissions
            .FirstOrDefaultAsync(x => x.UserId == r.UserId && x.PermissionId == r.PermissionId, ct);

        if (r.Mod == "yok")
        {
            if (mevcut is not null)
            {
                var eskiMod = mevcut.GrantType == "revoke" ? "kaldırma" : "verme";
                db.UserPermissions.Remove(mevcut);   // istisna kaydı tamamen kalkar (gruptan gelen kalır)
                await db.SaveChangesAsync(ct);
                await yetkiServisi.GecersizKilAsync(r.UserId, ct);
                await denetim.YazAsync(new YetkiDenetimKaydi(
                    "yetki.kullanici.istisna",
                    $"{await AdSoyad(db, r.UserId, ct)} kullanıcısındaki \"{perm.Code}\" {eskiMod} istisnasını kaldırdı " +
                    "(gruplardan gelen yetki geçerli olur).",
                    HedefKullaniciId: r.UserId, YetkiId: r.PermissionId, Oncesi: eskiMod, Sonrasi: "yok"), ct);
            }
            return Result.Success(true);
        }

        var kanallar = perm.ChannelScoped ? (r.ChannelIds ?? new List<Guid>()) : null;
        if (mevcut is null)
            db.UserPermissions.Add(new UserPermission
            {
                UserId = r.UserId, PermissionId = r.PermissionId,
                GrantType = r.Mod == "kaldir" ? "revoke" : "grant",
                ChannelIds = kanallar, CreatedBy = r.IsleyenId,
            });
        else
        {
            mevcut.GrantType = r.Mod == "kaldir" ? "revoke" : "grant";
            mevcut.ChannelIds = kanallar;
            mevcut.IsDeleted = false; mevcut.DeletedAt = null; mevcut.DeletedBy = null;
            mevcut.UpdatedAt = DateTime.UtcNow; mevcut.UpdatedBy = r.IsleyenId;
        }

        await db.SaveChangesAsync(ct);
        await yetkiServisi.GecersizKilAsync(r.UserId, ct);   // K3

        var kanalMetni = perm.ChannelScoped ? $" ({kanallar!.Count} kanal)" : "";
        await denetim.YazAsync(new YetkiDenetimKaydi(
            "yetki.kullanici.istisna",
            r.Mod == "ver"
                ? $"{await AdSoyad(db, r.UserId, ct)} kullanıcısına \"{perm.Code}\" yetkisini özel olarak verdi{kanalMetni}."
                : $"{await AdSoyad(db, r.UserId, ct)} kullanıcısında \"{perm.Code}\" yetkisini kapattı{kanalMetni}.",
            HedefKullaniciId: r.UserId, YetkiId: r.PermissionId,
            Sonrasi: $"{r.Mod}{kanalMetni}"), ct);

        return Result.Success(true);
    }

    private static async Task<string> AdSoyad(IIamDbContext db, Guid userId, CancellationToken ct) =>
        await db.Users.Where(u => u.Id == userId)
            .Select(u => (u.FirstName + " " + u.LastName).Trim()).FirstOrDefaultAsync(ct) ?? "";
}

// ── Katalog gösterim alanları (K4: kod sahipli; panel yalnız sunumu düzenler) ──
public record YetkiGosterimGuncelleCommand(Guid PermissionId, string Ad, string? Aciklama,
    string? Sayfa, int Sira, bool Aktif, bool? KanalKapsamli, Guid IsleyenId) : IRequest<Result<bool>>;

public class YetkiGosterimGuncelleCommandHandler(IIamDbContext db, IYetkiDenetimi denetim)
    : IRequestHandler<YetkiGosterimGuncelleCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(YetkiGosterimGuncelleCommand r, CancellationToken ct)
    {
        var p = await db.Permissions.FirstOrDefaultAsync(x => x.Id == r.PermissionId, ct);
        if (p is null) return Result.Failure<bool>("Yetki bulunamadı.");
        if (string.IsNullOrWhiteSpace(r.Ad)) return Result.Failure<bool>("Görünen ad boş olamaz.");

        p.NameI18n = new Dictionary<string, string> { ["tr"] = r.Ad.Trim() };
        p.DescriptionI18n = string.IsNullOrWhiteSpace(r.Aciklama)
            ? null : new Dictionary<string, string> { ["tr"] = r.Aciklama!.Trim() };
        p.PageCode = string.IsNullOrWhiteSpace(r.Sayfa) ? null : r.Sayfa!.Trim();
        p.SortOrder = r.Sira;
        p.IsActive = r.Aktif;

        // Kanal bayrağını panel değiştirdiyse artık katalog senkronu ezmez (açık bayrak).
        if (r.KanalKapsamli is { } kk && kk != p.ChannelScoped)
        {
            p.ChannelScoped = kk;
            p.ChannelScopedOverridden = true;
        }

        p.UpdatedAt = DateTime.UtcNow;
        p.UpdatedBy = r.IsleyenId;
        await db.SaveChangesAsync(ct);

        await denetim.YazAsync(new YetkiDenetimKaydi(
            "yetki.katalog.guncelle",
            $"\"{p.Code}\" yetkisinin gösterim bilgilerini güncelledi (ad: {r.Ad}, durum: {(r.Aktif ? "aktif" : "pasif")}).",
            YetkiId: p.Id), ct);

        return Result.Success(true);
    }
}

// ── Süper admin bayrağı (K5 + tasarım §K) ─────────────────────────────────────
/// <summary>
/// Süper adminlik verme/kaldırma. Kurallar:
///  • Yalnız süper admin yapabilir (controller katmanında da denetlenir).
///  • Kullanıcı KENDİ bayrağını kaldıramaz — kaldırma her zaman BAŞKA bir süper admin ister.
///  • Sistemde en az bir süper admin kalmalıdır.
///  • Olay ayrı ve vurgulu bir denetim kaydı üretir.
/// </summary>
public record SuperAdminDegistirCommand(Guid UserId, bool Deger, bool IsleyenSuperAdmin, Guid IsleyenId)
    : IRequest<Result<bool>>;

public class SuperAdminDegistirCommandHandler(
    IIamDbContext db, IEtkinYetkiServisi yetkiServisi, IYetkiDenetimi denetim)
    : IRequestHandler<SuperAdminDegistirCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(SuperAdminDegistirCommand r, CancellationToken ct)
    {
        if (!r.IsleyenSuperAdmin)
            return Result.Failure<bool>("Süper admin yetkisini yalnız bir süper admin verebilir/kaldırabilir.");

        var kullanici = await db.Users.FirstOrDefaultAsync(u => u.Id == r.UserId, ct);
        if (kullanici is null) return Result.Failure<bool>("Kullanıcı bulunamadı.");
        if (kullanici.IsSuperAdmin == r.Deger) return Result.Success(true);   // değişiklik yok

        if (!r.Deger)
        {
            if (r.UserId == r.IsleyenId)
                return Result.Failure<bool>(
                    "Kendi süper admin yetkinizi kaldıramazsınız; bunu başka bir süper admin yapmalıdır.");

            var kalan = await db.Users.CountAsync(u => u.IsSuperAdmin && u.IsActive && u.Id != r.UserId, ct);
            if (kalan == 0)
                return Result.Failure<bool>("Sistemde en az bir süper admin kalmalıdır.");
        }

        kullanici.IsSuperAdmin = r.Deger;
        kullanici.UpdatedAt = DateTime.UtcNow;
        kullanici.UpdatedBy = r.IsleyenId;
        await db.SaveChangesAsync(ct);
        await yetkiServisi.GecersizKilAsync(r.UserId, ct);

        var ad = (kullanici.FirstName + " " + kullanici.LastName).Trim();
        await denetim.YazAsync(new YetkiDenetimKaydi(
            r.Deger ? "yetki.superadmin.ver" : "yetki.superadmin.kaldir",
            r.Deger ? $"{ad} kullanıcısına SÜPER ADMİN yetkisi verdi."
                    : $"{ad} kullanıcısının SÜPER ADMİN yetkisini kaldırdı.",
            HedefKullaniciId: r.UserId,
            Oncesi: r.Deger ? "normal" : "süper admin",
            Sonrasi: r.Deger ? "süper admin" : "normal"), ct);

        return Result.Success(true);
    }
}

// ── Y8 (K8 adım 3-6): geçişten çıkış komutları ────────────────────────────────
//
// K8'in altı adımı: (1) geçiş grubu (2) kullanıcıları al (3) GERÇEK DEPARTMAN GRUPLARI
// (4) kullanıcıları taşı (5) istisnalar (6) geçiş grubunu kaldır.
// Adım 3 şablondan kurma, adım 4 kopyalama + üyelik, adım 6 grup kaldırma ile yapılır.

/// <summary>
/// Kod şablonundan departman grubu kurar (K8 adım 3). Şablon yalnız başlangıç değeridir:
/// kurulduktan sonra grup normal bir gruptur, şablon bir daha dokunmaz.
/// Aynı kodlu grup varsa hata döner (yanlışlıkla yetki geri yüklemeyi engeller).
/// </summary>
public record SablondanGrupOlusturCommand(string SablonKodu, List<Guid> TumKanallar,
    IsleyenKapsami Isleyen, Guid IsleyenId) : IRequest<Result<Guid>>;

public class SablondanGrupOlusturCommandHandler(
    IIamDbContext db, IEtkinYetkiServisi yetkiServisi, IYetkiDenetimi denetim)
    : IRequestHandler<SablondanGrupOlusturCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(SablondanGrupOlusturCommand r, CancellationToken ct)
    {
        var sablon = ECSPros.Shared.Kernel.Authorization.DepartmanGrupSablonlari.Bul(r.SablonKodu);
        if (sablon is null) return Result.Failure<Guid>("Şablon bulunamadı.");
        if (await db.Roles.AnyAsync(x => x.Code == sablon.Kod, ct))
            return Result.Failure<Guid>($"\"{sablon.Ad}\" grubu zaten kurulu; şablon yeniden uygulanmaz.");

        var katalog = await db.Permissions.AsNoTracking()
            .Where(p => p.IsActive && sablon.Yetkiler.Contains(p.Code))
            .Select(p => new { p.Id, p.Code, p.ChannelScoped })
            .ToListAsync(ct);

        // Eskalasyon (§O.4): şablon da olsa, kendi taşımadığın yetkiyi dağıtamazsın.
        var verilebilir = katalog
            .Where(p => r.Isleyen.Verebilir(p.Code, p.ChannelScoped ? r.TumKanallar : null))
            .ToList();
        if (verilebilir.Count == 0)
            return Result.Failure<Guid>("Bu şablondaki yetkilerin hiçbirini verme yetkiniz yok.");

        var grup = new Role
        {
            Code = sablon.Kod,
            NameI18n = new Dictionary<string, string> { ["tr"] = sablon.Ad },
            DescriptionI18n = new Dictionary<string, string> { ["tr"] = sablon.Aciklama },
            IsSystem = false,
            IsActive = true,
            CreatedBy = r.IsleyenId,
        };
        db.Roles.Add(grup);

        foreach (var p in verilebilir)
            db.RolePermissions.Add(new RolePermission
            {
                RoleId = grup.Id,
                PermissionId = p.Id,
                // "Tüm kanallar" = ANLIK liste (Ek-2): sonradan açılan kanal otomatik girmez.
                ChannelIds = p.ChannelScoped ? new List<Guid>(r.TumKanallar) : null,
                CreatedBy = r.IsleyenId,
            });

        await db.SaveChangesAsync(ct);

        var atlanan = katalog.Count - verilebilir.Count;
        await denetim.YazAsync(new YetkiDenetimKaydi(
            "yetki.grup.sablon",
            $"\"{sablon.Ad}\" departman grubunu şablondan kurdu ({verilebilir.Count} yetki" +
            (atlanan > 0 ? $"; kendi taşımadığı {atlanan} yetki atlandı" : "") + ").",
            HedefGrupId: grup.Id,
            Sonrasi: string.Join(", ", verilebilir.Select(p => p.Code).OrderBy(x => x))), ct);

        return Result.Success(grup.Id);
    }
}

/// <summary>
/// Grup kopyalar (K8 adım 4'ün pratik aracı): yetkiler ve kanal kümeleri birebir taşınır,
/// ÜYELER TAŞINMAZ — kopya boş bir gruptur. Eskalasyon denetimi kopyada da geçerlidir.
/// </summary>
public record GrupKopyalaCommand(Guid KaynakId, string YeniAd, IsleyenKapsami Isleyen, Guid IsleyenId)
    : IRequest<Result<Guid>>;

public class GrupKopyalaCommandHandler(IIamDbContext db, IYetkiDenetimi denetim)
    : IRequestHandler<GrupKopyalaCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(GrupKopyalaCommand r, CancellationToken ct)
    {
        var ad = r.YeniAd?.Trim() ?? "";
        if (ad.Length < 2) return Result.Failure<Guid>("Grup adı en az 2 karakter olmalıdır.");

        var kaynak = await db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == r.KaynakId, ct);
        if (kaynak is null) return Result.Failure<Guid>("Kaynak grup bulunamadı.");

        var kod = GrupKodu(ad);
        if (await db.Roles.AnyAsync(x => x.Code == kod, ct))
            return Result.Failure<Guid>($"'{kod}' kodlu bir grup zaten var; farklı bir ad seçin.");

        var yetkiler = await (
            from rp in db.RolePermissions.AsNoTracking().Where(x => x.RoleId == r.KaynakId && !x.IsDeleted)
            join p in db.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
            where p.IsActive
            select new { p.Id, p.Code, rp.ChannelIds }).ToListAsync(ct);

        var verilebilir = yetkiler.Where(y => r.Isleyen.Verebilir(y.Code, y.ChannelIds)).ToList();

        var kopya = new Role
        {
            Code = kod,
            NameI18n = new Dictionary<string, string> { ["tr"] = ad },
            DescriptionI18n = new Dictionary<string, string>
            { ["tr"] = $"\"{(kaynak.NameI18n.TryGetValue("tr", out var kad) ? kad : kaynak.Code)}\" grubundan kopyalandı." },
            IsSystem = false,
            IsActive = true,
            CreatedBy = r.IsleyenId,
        };
        db.Roles.Add(kopya);

        foreach (var y in verilebilir)
            db.RolePermissions.Add(new RolePermission
            {
                RoleId = kopya.Id,
                PermissionId = y.Id,
                ChannelIds = y.ChannelIds is null ? null : new List<Guid>(y.ChannelIds),
                CreatedBy = r.IsleyenId,
            });

        await db.SaveChangesAsync(ct);

        var atlanan = yetkiler.Count - verilebilir.Count;
        await denetim.YazAsync(new YetkiDenetimKaydi(
            "yetki.grup.kopyala",
            $"\"{(kaynak.NameI18n.TryGetValue("tr", out var k2) ? k2 : kaynak.Code)}\" grubunu " +
            $"\"{ad}\" adıyla kopyaladı ({verilebilir.Count} yetki" +
            (atlanan > 0 ? $"; kendi taşımadığı {atlanan} yetki atlandı" : "") + "; üyeler kopyalanmaz).",
            HedefGrupId: kopya.Id), ct);

        return Result.Success(kopya.Id);
    }

    internal static string GrupKodu(string ad)
    {
        var s = ad.Trim().ToLowerInvariant()
            .Replace('ı', 'i').Replace('ğ', 'g').Replace('ü', 'u')
            .Replace('ş', 's').Replace('ö', 'o').Replace('ç', 'c');
        var temiz = new string(s.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        while (temiz.Contains("__")) temiz = temiz.Replace("__", "_");
        return temiz.Trim('_');
    }
}

/// <summary>
/// Grubu kaldırır (K8 adım 6). Kurallar:
///  • ÜYESİ VARSA kaldırılmaz — önce kullanıcılar gerçek gruplarına taşınmalıdır
///    (bir grubu silmek, içindeki kişileri sessizce yetkisiz bırakmamalı).
///  • Sistem grubu (super_admin / platform_admin) kaldırılamaz.
///  • Soft delete: kayıt durur, denetim izi bozulmaz. Geçiş grubu bir daha
///    açılışta KURULMAZ (DatabaseSeeder silinmiş grubu yeniden oluşturmaz).
/// </summary>
public record GrupKaldirCommand(Guid GroupId, Guid IsleyenId) : IRequest<Result<bool>>;

public class GrupKaldirCommandHandler(IIamDbContext db, IEtkinYetkiServisi yetkiServisi, IYetkiDenetimi denetim)
    : IRequestHandler<GrupKaldirCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(GrupKaldirCommand r, CancellationToken ct)
    {
        var grup = await db.Roles.FirstOrDefaultAsync(x => x.Id == r.GroupId, ct);
        if (grup is null) return Result.Failure<bool>("Yetki grubu bulunamadı.");
        if (grup.IsSystem) return Result.Failure<bool>("Sistem grubu kaldırılamaz.");

        var uyeSayisi = await db.UserRoles.CountAsync(ur => ur.RoleId == r.GroupId && !ur.IsDeleted, ct);
        if (uyeSayisi > 0)
            return Result.Failure<bool>(
                $"Bu grupta {uyeSayisi} kullanıcı var. Önce onları gerçek gruplarına taşıyın, sonra grubu kaldırın.");

        var yetkiler = await db.RolePermissions.Where(rp => rp.RoleId == r.GroupId && !rp.IsDeleted).ToListAsync(ct);
        foreach (var rp in yetkiler)
        { rp.IsDeleted = true; rp.DeletedAt = DateTime.UtcNow; rp.DeletedBy = r.IsleyenId; }

        grup.IsActive = false;
        grup.IsDeleted = true;
        grup.DeletedAt = DateTime.UtcNow;
        grup.DeletedBy = r.IsleyenId;

        await db.SaveChangesAsync(ct);
        await yetkiServisi.GrupIcinGecersizKilAsync(r.GroupId, ct);

        var ad = grup.NameI18n.TryGetValue("tr", out var gad) ? gad : grup.Code;
        await denetim.YazAsync(new YetkiDenetimKaydi(
            "yetki.grup.kaldir",
            $"\"{ad}\" yetki grubunu kaldırdı ({yetkiler.Count} yetki düştü; grup boştu).",
            HedefGrupId: r.GroupId, Oncesi: "aktif", Sonrasi: "kaldırıldı"), ct);

        return Result.Success(true);
    }
}
