using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Authorization;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Yetkilendirme;

// Y4 (2026-09-09): yetkilendirme panelinin okuma uçları.
// Panel dili: "Yetki Grubu" = DB'deki Role. Kullanıcıya ALLOW/DENY/INHERIT gibi teknik kavram
// gösterilmez; ekranlar "yetki var mı / hangi kanallarda / nereden geliyor" sorularını yanıtlar.

/// <summary>Katalog satırı — kod sahipli alanlar salt okunur, gösterim alanları panelden düzenlenir.</summary>
public record YetkiKatalogSatiri(
    Guid Id, string Code, string Ad, string? Aciklama, string Modul, string? Sayfa,
    string Tur, bool KanalKapsamli, bool Aktif, bool KoddaTanimli, int Sira,
    int GrupSayisi, int KullaniciSayisi);

public record GetYetkiKataloguQuery(bool YalnizAktif = false) : IRequest<Result<List<YetkiKatalogSatiri>>>;

public class GetYetkiKataloguQueryHandler(IIamDbContext db)
    : IRequestHandler<GetYetkiKataloguQuery, Result<List<YetkiKatalogSatiri>>>
{
    public async Task<Result<List<YetkiKatalogSatiri>>> Handle(GetYetkiKataloguQuery request, CancellationToken ct)
    {
        var q = db.Permissions.AsNoTracking().AsQueryable();
        if (request.YalnizAktif) q = q.Where(p => p.IsActive);

        var satirlar = await q
            .OrderBy(p => p.Module).ThenBy(p => p.SortOrder).ThenBy(p => p.Code)
            .Select(p => new YetkiKatalogSatiri(
                p.Id, p.Code,
                p.NameI18n.ContainsKey("tr") ? p.NameI18n["tr"] : p.Code,
                p.DescriptionI18n != null && p.DescriptionI18n.ContainsKey("tr") ? p.DescriptionI18n["tr"] : null,
                p.Module, p.PageCode, p.Kind, p.ChannelScoped, p.IsActive, p.IsCodeDefined, p.SortOrder,
                db.RolePermissions.Count(rp => rp.PermissionId == p.Id && !rp.IsDeleted),
                db.UserPermissions.Count(up => up.PermissionId == p.Id && !up.IsDeleted)))
            .ToListAsync(ct);

        return Result.Success(satirlar);
    }
}

/// <summary>Yetki grubu listesi.</summary>
public record YetkiGrubuSatiri(Guid Id, string Code, string Ad, string? Aciklama, bool Aktif,
    bool Sistem, int KullaniciSayisi, int YetkiSayisi, bool GecisGrubu);

public record GetYetkiGruplariQuery : IRequest<Result<List<YetkiGrubuSatiri>>>;

public class GetYetkiGruplariQueryHandler(IIamDbContext db)
    : IRequestHandler<GetYetkiGruplariQuery, Result<List<YetkiGrubuSatiri>>>
{
    public async Task<Result<List<YetkiGrubuSatiri>>> Handle(GetYetkiGruplariQuery request, CancellationToken ct)
        => Result.Success(await db.Roles.AsNoTracking()
            .OrderBy(r => r.Code)
            .Select(r => new YetkiGrubuSatiri(
                r.Id, r.Code,
                r.NameI18n.ContainsKey("tr") ? r.NameI18n["tr"] : r.Code,
                r.DescriptionI18n != null && r.DescriptionI18n.ContainsKey("tr") ? r.DescriptionI18n["tr"] : null,
                r.IsActive, r.IsSystem,
                db.UserRoles.Count(ur => ur.RoleId == r.Id && !ur.IsDeleted),
                db.RolePermissions.Count(rp => rp.RoleId == r.Id && !rp.IsDeleted),
                r.Code == "gecis_tam_erisim"))
            .ToListAsync(ct));
}

/// <summary>Grup detayı: verdiği yetkiler (kanal kümeleriyle) + üyeler.</summary>
public record GrupYetkisi(Guid PermissionId, string Code, List<Guid> Kanallar);
public record GrupUyesi(Guid UserId, string AdSoyad, string Email, bool SuperAdmin);
public record YetkiGrubuDetay(Guid Id, string Code, string Ad, string? Aciklama, bool Aktif, bool Sistem,
    List<GrupYetkisi> Yetkiler, List<GrupUyesi> Uyeler);

public record GetYetkiGrubuDetayQuery(Guid GroupId) : IRequest<Result<YetkiGrubuDetay>>;

public class GetYetkiGrubuDetayQueryHandler(IIamDbContext db)
    : IRequestHandler<GetYetkiGrubuDetayQuery, Result<YetkiGrubuDetay>>
{
    public async Task<Result<YetkiGrubuDetay>> Handle(GetYetkiGrubuDetayQuery request, CancellationToken ct)
    {
        var grup = await db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == request.GroupId, ct);
        if (grup is null) return Result.Failure<YetkiGrubuDetay>("Yetki grubu bulunamadı.");

        var yetkiler = await (
            from rp in db.RolePermissions.AsNoTracking().Where(x => x.RoleId == request.GroupId && !x.IsDeleted)
            join p in db.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
            select new GrupYetkisi(p.Id, p.Code, rp.ChannelIds ?? new List<Guid>()))
            .ToListAsync(ct);

        var uyeler = await (
            from ur in db.UserRoles.AsNoTracking().Where(x => x.RoleId == request.GroupId && !x.IsDeleted)
            join u in db.Users.AsNoTracking() on ur.UserId equals u.Id
            select new GrupUyesi(u.Id, (u.FirstName + " " + u.LastName).Trim(), u.Email, u.IsSuperAdmin))
            .ToListAsync(ct);

        return Result.Success(new YetkiGrubuDetay(
            grup.Id, grup.Code,
            grup.NameI18n.TryGetValue("tr", out var ad) ? ad : grup.Code,
            grup.DescriptionI18n is not null && grup.DescriptionI18n.TryGetValue("tr", out var ac) ? ac : null,
            grup.IsActive, grup.IsSystem, yetkiler, uyeler));
    }
}

/// <summary>
/// Kullanıcının EFEKTİF yetkileri + KAYNAK bilgisi (tasarım §H: "neden yapabiliyor / neden yapamıyor?").
/// Kapalı yetkiler de döner: sebep "kaynak yok" / "istisnayla kaldırıldı" / "yetki pasif".
/// </summary>
public record KullaniciYetkiSatiri(
    Guid PermissionId, string Code, string Ad, string Modul, string? Sayfa, string Tur,
    bool KanalKapsamli, bool Acik, List<Guid> Kanallar, List<string> Kaynaklar, string? KapaliSebebi);

public record KullaniciIstisnasi(Guid PermissionId, string Code, string Ad, string Mod, List<Guid> Kanallar);

public record KullaniciYetkileri(
    Guid UserId, string AdSoyad, string Email, bool Aktif, bool SuperAdmin,
    List<YetkiGrubuSatiri> Gruplar, List<KullaniciYetkiSatiri> Yetkiler, List<KullaniciIstisnasi> Istisnalar);

public record GetKullaniciYetkileriQuery(Guid UserId) : IRequest<Result<KullaniciYetkileri>>;

public class GetKullaniciYetkileriQueryHandler(IIamDbContext db)
    : IRequestHandler<GetKullaniciYetkileriQuery, Result<KullaniciYetkileri>>
{
    public async Task<Result<KullaniciYetkileri>> Handle(GetKullaniciYetkileriQuery request, CancellationToken ct)
    {
        var kullanici = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.UserId, ct);
        if (kullanici is null) return Result.Failure<KullaniciYetkileri>("Kullanıcı bulunamadı.");

        var gruplar = await (
            from ur in db.UserRoles.AsNoTracking().Where(x => x.UserId == request.UserId && !x.IsDeleted)
            join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
            select new YetkiGrubuSatiri(r.Id, r.Code,
                r.NameI18n.ContainsKey("tr") ? r.NameI18n["tr"] : r.Code, null, r.IsActive, r.IsSystem,
                0, 0, r.Code == "gecis_tam_erisim"))
            .ToListAsync(ct);

        var gruptan = await (
            from ur in db.UserRoles.AsNoTracking().Where(x => x.UserId == request.UserId && !x.IsDeleted)
            join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
            join rp in db.RolePermissions.AsNoTracking().Where(x => !x.IsDeleted) on r.Id equals rp.RoleId
            join p in db.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
            select new
            {
                p.Id, p.Code, p.ChannelScoped, p.IsActive, rp.ChannelIds,
                GrupAd = r.NameI18n.ContainsKey("tr") ? r.NameI18n["tr"] : r.Code,
            }).ToListAsync(ct);

        var istisnalar = await (
            from up in db.UserPermissions.AsNoTracking().Where(x => x.UserId == request.UserId && !x.IsDeleted)
            join p in db.Permissions.AsNoTracking() on up.PermissionId equals p.Id
            select new
            {
                p.Id, p.Code, p.ChannelScoped, p.IsActive, up.ChannelIds, up.GrantType,
                Ad = p.NameI18n.ContainsKey("tr") ? p.NameI18n["tr"] : p.Code,
            }).ToListAsync(ct);

        var katalog = await db.Permissions.AsNoTracking()
            .OrderBy(p => p.Module).ThenBy(p => p.SortOrder).ThenBy(p => p.Code)
            .Select(p => new
            {
                p.Id, p.Code, p.Module, p.PageCode, p.Kind, p.ChannelScoped, p.IsActive,
                Ad = p.NameI18n.ContainsKey("tr") ? p.NameI18n["tr"] : p.Code,
            }).ToListAsync(ct);

        // Efektif hesap: TEK kural (EfektifYetkiHesabi) — ekran ile gerçek davranış ayrışmasın.
        var kaynaklar = new List<YetkiKaynagi>();
        foreach (var g in gruptan.Where(x => x.IsActive))
            kaynaklar.Add(new YetkiKaynagi(g.Code, g.ChannelScoped, g.ChannelIds, YetkiKaynakTipi.Grup));
        foreach (var i in istisnalar.Where(x => x.IsActive))
            kaynaklar.Add(new YetkiKaynagi(i.Code, i.ChannelScoped, i.ChannelIds,
                i.GrantType == "revoke" ? YetkiKaynakTipi.KullaniciKaldir : YetkiKaynakTipi.KullaniciVer));

        var efektif = EfektifYetkiHesabi.Hesapla(kullanici.IsSuperAdmin, kaynaklar);

        var satirlar = new List<KullaniciYetkiSatiri>();
        foreach (var p in katalog)
        {
            var acik = kullanici.IsSuperAdmin || efektif.Var(p.Code);
            var kanallar = efektif.Kanallar(p.Code)?.ToList() ?? new List<Guid>();

            var kaynakAdlari = new List<string>();
            foreach (var g in gruptan.Where(x => x.Code == p.Code && x.IsActive))
                if (!kaynakAdlari.Contains(g.GrupAd)) kaynakAdlari.Add(g.GrupAd);
            if (istisnalar.Any(x => x.Code == p.Code && x.GrantType != "revoke" && x.IsActive))
                kaynakAdlari.Add("Kullanıcıya özel");
            if (kullanici.IsSuperAdmin) kaynakAdlari.Add("Süper admin");

            string? kapaliSebebi = null;
            if (!acik)
            {
                if (!p.IsActive) kapaliSebebi = "Yetki pasif";
                else if (istisnalar.Any(x => x.Code == p.Code && x.GrantType == "revoke"))
                    kapaliSebebi = "Kullanıcı istisnasıyla kaldırıldı";
                else if (gruptan.Any(x => x.Code == p.Code))
                    kapaliSebebi = "Gruptan geliyor ama kanal kapsamı boş";
                else kapaliSebebi = "Hiçbir kaynaktan gelmiyor";
            }

            satirlar.Add(new KullaniciYetkiSatiri(
                p.Id, p.Code, p.Ad, p.Module, p.PageCode, p.Kind, p.ChannelScoped,
                acik, kanallar, kaynakAdlari, kapaliSebebi));
        }

        return Result.Success(new KullaniciYetkileri(
            kullanici.Id, (kullanici.FirstName + " " + kullanici.LastName).Trim(), kullanici.Email,
            kullanici.IsActive, kullanici.IsSuperAdmin, gruplar, satirlar,
            istisnalar.Select(i => new KullaniciIstisnasi(i.Id, i.Code, i.Ad,
                i.GrantType == "revoke" ? "kaldir" : "ver", i.ChannelIds ?? new List<Guid>())).ToList()));
    }
}

// ── Yetki Logları (Y5, tasarım §J.3) ──────────────────────────────────────────
/// <summary>Panelde insan diliyle okunan yetki olayı.</summary>
public record YetkiLogSatiri(
    Guid Id, DateTime Tarih, string Olay, string Ozet,
    string? Aktor, string? HedefKullanici, string? HedefGrup, string? Yetki,
    string? Oncesi, string? Sonrasi, string? Ip);

public record GetYetkiLoglariQuery(
    DateTime? Baslangic = null, DateTime? Bitis = null, Guid? AktorId = null,
    Guid? HedefKullaniciId = null, Guid? HedefGrupId = null, string? Olay = null,
    string? Arama = null, int Page = 1, int PageSize = 50)
    : IRequest<Result<PagedResult<YetkiLogSatiri>>>;

public class GetYetkiLoglariQueryHandler(IIamDbContext db)
    : IRequestHandler<GetYetkiLoglariQuery, Result<PagedResult<YetkiLogSatiri>>>
{
    public async Task<Result<PagedResult<YetkiLogSatiri>>> Handle(GetYetkiLoglariQuery r, CancellationToken ct)
    {
        var q = db.AuditLogs.AsNoTracking().Where(a => a.EntityType.StartsWith("yetki."));

        if (r.Baslangic is { } b) q = q.Where(a => a.CreatedAt >= b);
        if (r.Bitis is { } s) q = q.Where(a => a.CreatedAt <= s);
        if (r.AktorId is { } ak) q = q.Where(a => a.UserId == ak);
        if (!string.IsNullOrWhiteSpace(r.Olay)) q = q.Where(a => a.EntityType == r.Olay);
        // Hedef (kullanıcı/grup/yetki) EntityId'de tutulur; filtre doğrudan onun üzerinden.
        if (r.HedefKullaniciId is { } hk) q = q.Where(a => a.EntityId == hk);
        if (r.HedefGrupId is { } hg) q = q.Where(a => a.EntityId == hg);

        var toplam = await q.CountAsync(ct);
        var sayfa = Math.Max(1, r.Page);
        var boy = Math.Clamp(r.PageSize, 1, 200);

        var ham = await q.OrderByDescending(a => a.CreatedAt)
            .Skip((sayfa - 1) * boy).Take(boy)
            .Select(a => new { a.Id, a.CreatedAt, a.EntityType, a.UserId, a.EntityId, a.Context, a.OldValues, a.NewValues, a.IpAddress })
            .ToListAsync(ct);

        var aktorIdler = ham.Where(x => x.UserId.HasValue).Select(x => x.UserId!.Value).Distinct().ToList();
        var hedefIdler = ham.Select(x => x.EntityId).Distinct().ToList();

        var kullanicilar = await db.Users.AsNoTracking()
            .Where(u => aktorIdler.Contains(u.Id) || hedefIdler.Contains(u.Id))
            .Select(u => new { u.Id, Ad = (u.FirstName + " " + u.LastName).Trim() })
            .ToDictionaryAsync(u => u.Id, u => u.Ad, ct);
        var gruplar = await db.Roles.AsNoTracking()
            .Where(g => hedefIdler.Contains(g.Id))
            .Select(g => new { g.Id, Ad = g.NameI18n.ContainsKey("tr") ? g.NameI18n["tr"] : g.Code })
            .ToDictionaryAsync(g => g.Id, g => g.Ad, ct);
        var yetkiler = await db.Permissions.AsNoTracking()
            .Where(p => hedefIdler.Contains(p.Id))
            .Select(p => new { p.Id, p.Code })
            .ToDictionaryAsync(p => p.Id, p => p.Code, ct);

        string? Metin(Dictionary<string, object>? d) =>
            d is not null && d.TryGetValue("deger", out var v) ? v?.ToString() : null;

        var satirlar = ham.Select(a =>
        {
            var ozet = a.Context is not null && a.Context.TryGetValue("ozet", out var o)
                ? o?.ToString() ?? a.EntityType : a.EntityType;
            return new YetkiLogSatiri(
                a.Id, a.CreatedAt, a.EntityType, ozet,
                a.UserId is { } u && kullanicilar.TryGetValue(u, out var aktorAd) ? aktorAd : null,
                kullanicilar.TryGetValue(a.EntityId, out var hkAd) ? hkAd : null,
                gruplar.TryGetValue(a.EntityId, out var grupAd) ? grupAd : null,
                yetkiler.TryGetValue(a.EntityId, out var yetkiKod) ? yetkiKod : null,
                Metin(a.OldValues), Metin(a.NewValues), a.IpAddress);
        }).ToList();

        // Arama: özet metninde (bellekte — sayfa başına en çok 200 satır)
        if (!string.IsNullOrWhiteSpace(r.Arama))
        {
            var terim = r.Arama.Trim().ToLowerInvariant();
            satirlar = satirlar.Where(x => x.Ozet.ToLowerInvariant().Contains(terim)).ToList();
        }

        return Result.Success(new PagedResult<YetkiLogSatiri>(satirlar, toplam, sayfa, boy));
    }
}

// ── Kullanıcı simülasyonu (Y7, tasarım §I) ────────────────────────────────────
/// <summary>
/// "Bu kullanıcı panelde ne görüyor?" — SALT OKUNUR önizleme.
/// Kullanıcı hesabına GİRİŞ (impersonation) yoktur: yalnız hesabın sonucu gösterilir, böylece
/// simülasyon sırasında yanlışlıkla işlem yapılamaz. Kaynak, gerçek davranışla ayrışmasın diye
/// <see cref="EfektifYetkiHesabi"/>'nın ta kendisidir (ayrı bir hesap yolu yazılmaz).
/// Çağıran ayrı bir yetki ister (<c>iam.permissions.simulate</c>) ve her çalıştırma LOGLANIR.
/// </summary>
public record SimulasyonYetkisi(string Code, string Ad, string Modul, string? Sayfa, string Tur,
    List<string> Kanallar, bool TumKanallar);

public record SimulasyonSonucu(
    Guid UserId, string AdSoyad, string Email, bool Aktif, bool SuperAdmin,
    List<string> Gruplar,
    List<SimulasyonYetkisi> Sayfalar,      // menüde görebileceği sayfalar
    List<SimulasyonYetkisi> Aksiyonlar,    // sayfalarda kullanabileceği işlemler
    List<string> GorunenAlanlar,           // hassas alanlardan görebildikleri
    List<string> GizliAlanlar,             // göremedikleri
    List<string> Kanallar,                 // erişebildiği kanal adları (birleşim)
    List<string> TumYetkiKodlari);         // panelin menü filtresini birebir uygulaması için

public record GetKullaniciSimulasyonQuery(Guid UserId) : IRequest<Result<SimulasyonSonucu>>;

public class GetKullaniciSimulasyonQueryHandler(IIamDbContext db, IEtkinYetkiServisi yetkiServisi)
    : IRequestHandler<GetKullaniciSimulasyonQuery, Result<SimulasyonSonucu>>
{
    public async Task<Result<SimulasyonSonucu>> Handle(GetKullaniciSimulasyonQuery r, CancellationToken ct)
    {
        var kullanici = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == r.UserId, ct);
        if (kullanici is null) return Result.Failure<SimulasyonSonucu>("Kullanıcı bulunamadı.");

        // Gerçek servis: simülasyon ile canlı davranış ayrışamaz.
        var efektif = await yetkiServisi.GetirAsync(r.UserId, ct);

        var katalog = await db.Permissions.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Module).ThenBy(p => p.SortOrder)
            .Select(p => new
            {
                p.Code, p.Module, p.PageCode, p.Kind, p.ChannelScoped,
                Ad = p.NameI18n.ContainsKey("tr") ? p.NameI18n["tr"] : p.Code,
            }).ToListAsync(ct);

        var gruplar = await (
            from ur in db.UserRoles.AsNoTracking().Where(x => x.UserId == r.UserId && !x.IsDeleted)
            join g in db.Roles.AsNoTracking() on ur.RoleId equals g.Id
            select g.NameI18n.ContainsKey("tr") ? g.NameI18n["tr"] : g.Code).ToListAsync(ct);

        var sayfalar = new List<SimulasyonYetkisi>();
        var aksiyonlar = new List<SimulasyonYetkisi>();
        var gorunen = new List<string>();
        var gizli = new List<string>();
        var kodlar = new List<string>();
        var kanalIdleri = new HashSet<Guid>();

        foreach (var p in katalog)
        {
            var var_ = efektif.Var(p.Code);
            if (p.Kind == "field")
            {
                (var_ ? gorunen : gizli).Add(p.Ad);
                if (var_) kodlar.Add(p.Code);
                continue;
            }
            if (!var_) continue;

            kodlar.Add(p.Code);
            var kanallar = efektif.Kanallar(p.Code)?.ToList() ?? new List<Guid>();
            foreach (var k in kanallar) kanalIdleri.Add(k);

            var satir = new SimulasyonYetkisi(p.Code, p.Ad, p.Module, p.PageCode, p.Kind,
                new List<string>(), TumKanallar: !p.ChannelScoped || efektif.SuperAdmin);
            (p.Kind == "page" ? sayfalar : aksiyonlar).Add(satir with { Kanallar = kanallar.Select(x => x.ToString()).ToList() });
        }

        return Result.Success(new SimulasyonSonucu(
            kullanici.Id, (kullanici.FirstName + " " + kullanici.LastName).Trim(), kullanici.Email,
            kullanici.IsActive, kullanici.IsSuperAdmin, gruplar,
            sayfalar, aksiyonlar, gorunen, gizli,
            kanalIdleri.Select(x => x.ToString()).ToList(), kodlar));
    }
}

// ── Y8 (K8 adım 3 + geçiş panosu) ─────────────────────────────────────────────

/// <summary>Kurulabilir departman şablonu; <paramref name="Kurulu"/> ise grup zaten var.</summary>
public record GrupSablonuSatiri(string Kod, string Ad, string Aciklama, int YetkiSayisi,
    bool Kurulu, Guid? GrupId);

public record GetGrupSablonlariQuery : IRequest<Result<List<GrupSablonuSatiri>>>;

public class GetGrupSablonlariQueryHandler(IIamDbContext db)
    : IRequestHandler<GetGrupSablonlariQuery, Result<List<GrupSablonuSatiri>>>
{
    public async Task<Result<List<GrupSablonuSatiri>>> Handle(GetGrupSablonlariQuery request, CancellationToken ct)
    {
        var kodlar = ECSPros.Shared.Kernel.Authorization.DepartmanGrupSablonlari.Tumu.Select(s => s.Kod).ToList();
        var mevcut = await db.Roles.AsNoTracking()
            .Where(r => kodlar.Contains(r.Code))
            .Select(r => new { r.Code, r.Id })
            .ToListAsync(ct);
        // Katalogda karşılığı olmayan key şablondan sessizce düşer; sayaç GERÇEK sayıyı gösterir.
        var aktifKodlar = await db.Permissions.AsNoTracking()
            .Where(p => p.IsActive).Select(p => p.Code).ToListAsync(ct);

        return Result.Success(ECSPros.Shared.Kernel.Authorization.DepartmanGrupSablonlari.Tumu
            .Select(s =>
            {
                var kurulu = mevcut.FirstOrDefault(m => m.Code == s.Kod);
                return new GrupSablonuSatiri(s.Kod, s.Ad, s.Aciklama,
                    s.Yetkiler.Count(k => aktifKodlar.Contains(k)),
                    kurulu is not null, kurulu?.Id);
            })
            .ToList());
    }
}

/// <summary>
/// K8 geçiş panosu: "kimler hâlâ tam erişimde, geçiş grubu boşaldı mı?" sorusunun tek yanıtı.
/// Y8'in bitiş ölçütü buradan okunur; tahmin ettirmez.
/// </summary>
public record GecisDurumuKullanicisi(Guid UserId, string AdSoyad, string Email, string? Departman,
    bool SuperAdmin, List<string> Gruplar, int YetkiSayisi);

public record GecisDurumu(
    int AktifKullanici, int SuperAdminSayisi, int TamErisimliKullanici,
    int GecisGrubuUyesi, bool GecisGrubuVar, Guid? GecisGrubuId,
    int AktifYetkiSayisi, List<GecisDurumuKullanicisi> DikkatGerektiren);

public record GetGecisDurumuQuery : IRequest<Result<GecisDurumu>>;

public class GetGecisDurumuQueryHandler(IIamDbContext db)
    : IRequestHandler<GetGecisDurumuQuery, Result<GecisDurumu>>
{
    public async Task<Result<GecisDurumu>> Handle(GetGecisDurumuQuery request, CancellationToken ct)
    {
        var aktifYetki = await db.Permissions.CountAsync(p => p.IsActive, ct);
        var gecisGrubu = await db.Roles.AsNoTracking()
            .Where(r => r.Code == "gecis_tam_erisim")
            .Select(r => new { r.Id }).FirstOrDefaultAsync(ct);

        var kullanicilar = await db.Users.AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => new
            {
                u.Id,
                AdSoyad = (u.FirstName + " " + u.LastName).Trim(),
                u.Email,
                u.Department,
                u.IsSuperAdmin,
                Gruplar = db.UserRoles.Where(ur => ur.UserId == u.Id && !ur.IsDeleted)
                    .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r)
                    .Select(r => new { r.Id, r.Code, Ad = r.NameI18n.ContainsKey("tr") ? r.NameI18n["tr"] : r.Code })
                    .ToList(),
            })
            .ToListAsync(ct);

        var grupYetkiSayilari = await db.RolePermissions.AsNoTracking()
            .Where(rp => !rp.IsDeleted)
            .GroupBy(rp => rp.RoleId)
            .Select(g => new { RoleId = g.Key, Sayi = g.Select(x => x.PermissionId).Distinct().Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Sayi, ct);

        var satirlar = kullanicilar.Select(u =>
        {
            var yetki = u.IsSuperAdmin
                ? aktifYetki
                : u.Gruplar.Select(g => grupYetkiSayilari.TryGetValue(g.Id, out var s) ? s : 0)
                    .DefaultIfEmpty(0).Max();
            return new GecisDurumuKullanicisi(u.Id, u.AdSoyad, u.Email, u.Department, u.IsSuperAdmin,
                u.Gruplar.Select(g => g.Ad).ToList(), yetki);
        }).ToList();

        // "Dikkat gerektiren" = süper admin OLMADAN tam/tama yakın (%90+) erişim taşıyanlar:
        // K8 adım 4'te asıl taşınması gerekenler bunlardır.
        var esik = (int)Math.Ceiling(aktifYetki * 0.9);
        var dikkat = satirlar
            .Where(s => !s.SuperAdmin && s.YetkiSayisi >= esik)
            .OrderByDescending(s => s.YetkiSayisi).ThenBy(s => s.AdSoyad)
            .ToList();

        return Result.Success(new GecisDurumu(
            AktifKullanici: satirlar.Count,
            SuperAdminSayisi: satirlar.Count(s => s.SuperAdmin),
            TamErisimliKullanici: satirlar.Count(s => s.SuperAdmin || s.YetkiSayisi >= esik),
            GecisGrubuUyesi: gecisGrubu is null ? 0
                : await db.UserRoles.CountAsync(ur => ur.RoleId == gecisGrubu.Id && !ur.IsDeleted, ct),
            GecisGrubuVar: gecisGrubu is not null,
            GecisGrubuId: gecisGrubu?.Id,
            AktifYetkiSayisi: aktifYetki,
            DikkatGerektiren: dikkat));
    }
}
