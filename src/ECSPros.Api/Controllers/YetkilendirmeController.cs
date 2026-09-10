using ECSPros.Shared.Kernel.Grid;
using ECSPros.Api.Authorization;
using ECSPros.Iam.Application.Services;
using ECSPros.Iam.Application.Yetkilendirme;
using ECSPros.Shared.Kernel.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>
/// Y4 (2026-09-09): yetkilendirme panelinin uçları — Yetki İçerikleri, Yetki Grupları,
/// Kullanıcı Yetkileri. Tümü <c>iam.permissions.manage</c> ister; süper admin bayrakla geçer.
///
/// Eskalasyon koruması (tasarım §O.4) komutlarda: yetkilendiren kullanıcı YALNIZ kendi taşıdığı
/// yetkileri ve kanalları dağıtabilir. İşleyenin kapsamı burada çözülüp komuta verilir.
/// </summary>
[ApiController]
[Route("api/iam")]
[Authorize]
[RequirePermission(Permissions.IamPermissionsManage)]
public class YetkilendirmeController(
    IMediator mediator,
    IEtkinYetkiServisi yetkiServisi,
    ECSPros.Core.Application.Services.ICoreDbContext coreDb) : ControllerBase
{
    /// <summary>
    /// "Tüm kanallar" kısayolu (Ek-2): istekte kanal verilmediğinde O ANKİ aktif kanalların listesi
    /// yazılır. Kritik ayrım: kanal kapsamlı bir yetkide BOŞ liste "hiçbir kanal" demektir —
    /// örneğin "bu kullanıcıda kapat" isteği kanalsız gelirse hiçbir şeyi kapatmazdı.
    /// </summary>
    private async Task<List<Guid>> TumKanallarAsync(CancellationToken ct) =>
        await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            coreDb.FirmPlatforms.Where(fp => fp.IsActive).Select(fp => fp.Id), ct);

    private Guid IsleyenId =>
        Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;

    private async Task<IsleyenKapsami> IsleyenKapsamiAsync(CancellationToken ct)
    {
        var superAdmin = User.FindFirst("sa")?.Value == "true";
        if (superAdmin || IsleyenId == Guid.Empty)
            return new IsleyenKapsami(true, new Dictionary<string, IReadOnlyCollection<Guid>?>());

        var e = await yetkiServisi.GetirAsync(IsleyenId, ct);
        var d = new Dictionary<string, IReadOnlyCollection<Guid>?>(StringComparer.Ordinal);
        foreach (var key in e.Keyler) d[key] = e.Kanallar(key)?.ToList();
        return new IsleyenKapsami(e.SuperAdmin, d);
    }

    private IActionResult Sonuc<T>(ECSPros.Shared.Kernel.Common.Result<T> r) =>
        r.IsFailure ? BadRequest(new { success = false, error = r.Error })
                    : Ok(new { success = true, data = r.Value });

    // ── Kanal listesi (yetki ekranları) ───────────────────────────────────────
    /// <summary>
    /// Kanal kapsamlı yetkilerde seçilecek kanal listesi (2026-09-10): yetki grubu ve kullanıcı
    /// yetkisi ekranları bunu bekler. Panel daha önce var olmayan <c>GET /api/core/firm-platforms</c>'u
    /// çağırıyordu (404 → boş liste → kanal seçimi yapılamıyordu). CoreController'a konmadı: o
    /// denetleyici sınıf düzeyinde <c>definitions.view</c> ister, yetki yöneticisinde bu yetki
    /// olmayabilir. Kaynak <see cref="TumKanallarAsync"/> ile AYNI (aktif kanallar) — "tüm kanallar
    /// (bugünkü liste)" kısayolunun yazdığı küme ile ekrandaki liste ayrışamaz.
    /// </summary>
    [HttpGet("permission-channels")]
    public async Task<IActionResult> KanalListesi(CancellationToken ct)
    {
        var kanallar = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            coreDb.FirmPlatforms
                .Where(fp => fp.IsActive)
                .OrderBy(fp => fp.Code)
                .Select(fp => new { fp.Id, fp.Code, fp.NameI18n }), ct);
        return Ok(new { success = true, data = kanallar });
    }

    // ── Yetki İçerikleri (katalog) ────────────────────────────────────────────
    /// <summary>Yetki kataloğunu TAM liste olarak döner — yetki grubu ve kullanıcı yetkisi ekranları
    /// bunu bekler. ⚠ Sayfalanmaz; liste EKRANI için /permissions/grid kullanın.</summary>
    [HttpGet("permissions")]
    public async Task<IActionResult> Katalog([FromQuery] bool activeOnly = false, CancellationToken ct = default)
        => Sonuc(await mediator.Send(new GetYetkiKataloguQuery(activeOnly), ct));

    /// <summary>Yetki kataloğu liste ekranı (DataGrid): sayfalı + f.* filtreleri + sort/dir.</summary>
    [HttpGet("permissions/grid")]
    public async Task<IActionResult> KatalogGrid(
        [FromQuery] bool activeOnly = false, [FromQuery] string? tur = null,
        [FromQuery] bool yalnizSorunlu = false, [FromQuery] string? search = null, CancellationToken ct = default)
    {
        // Yetki tanımı kanaldan bağımsızdır (kanal kapsamı yetkinin ÖZELLİĞİ) → kanal kısıtı null.
        var grid = ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query, null, defaultPageSize: 50);
        return Sonuc(await mediator.Send(new GetYetkiKataloguGridQuery(
            new YetkiKatalogFiltreleri(activeOnly, tur, yalnizSorunlu, search), grid.Page, grid.PageSize, grid), ct));
    }

    /// <summary>Yetki kataloğunu Excel'e aktarır (DataGrid).</summary>
    [HttpPost("permissions/export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("grid-export")]
    public async Task<IActionResult> KatalogExport(
        [FromServices] ECSPros.Api.Authorization.IAlanYetkileri alanYetkileri, [FromBody] GridExportRequest body,
        [FromServices] IConfiguration config, [FromServices] ECSPros.Iam.Application.Services.IIamDbContext iam,
        [FromServices] ILogger<YetkilendirmeController> logger, CancellationToken ct)
    {
        var filtreler = new YetkiKatalogFiltreleri(
            body.NamedValue("activeOnly") == "true", body.NamedValue("tur"),
            body.NamedValue("yalnizSorunlu") == "true", body.Search);
        return await ECSPros.Api.Grid.GridExportEndpoint.RunAsync(this, body, config, iam, logger, "permission-catalog", "yetki-icerikleri", "Yetki İçerikleri",
            ECSPros.Api.Grid.YetkiKatalogExportColumns.All,
            max => mediator.Send(new ExportYetkiKataloguQuery(filtreler, body.ToGridRequest(null), max), ct),
            ct, alanIzinleri: await alanYetkileri.IzinlerAsync(ct));
    }

    /// <summary>Yalnız GÖSTERİM alanları düzenlenir (K4: teknik key ve tür koda aittir).</summary>
    [HttpPut("permissions/{id:guid}")]
    public async Task<IActionResult> YetkiGosterim(Guid id, [FromBody] YetkiGosterimRequest req, CancellationToken ct)
        => Sonuc(await mediator.Send(new YetkiGosterimGuncelleCommand(
            id, req.Ad, req.Aciklama, req.Sayfa, req.Sira, req.Aktif, req.KanalKapsamli, IsleyenId), ct));

    // ── Yetki Grupları ────────────────────────────────────────────────────────
    /// <summary>TAM grup listesi — Kullanıcı Yetkileri ekranının grup seçim kaynağı.
    /// ⚠ Sayfalanmaz; liste EKRANI için /permission-groups/grid kullanın.</summary>
    [HttpGet("permission-groups")]
    public async Task<IActionResult> Gruplar(CancellationToken ct)
        => Sonuc(await mediator.Send(new GetYetkiGruplariQuery(), ct));

    /// <summary>Yetki grupları liste ekranı (DataGrid): sayfalı + f.* filtreleri + sort/dir.
    /// ⚠ Düz <c>GET /permission-groups</c> sayfalanmaz — Kullanıcı Yetkileri ekranının grup kaynağı odur.</summary>
    [HttpGet("permission-groups/grid")]
    public async Task<IActionResult> GruplarGrid(
        [FromQuery] bool activeOnly = false, [FromQuery] bool sistemHaric = false,
        [FromQuery] string? search = null, CancellationToken ct = default)
    {
        // Grup tanımı kanaldan bağımsızdır (kanal kapsamı grubun VERDİĞİ yetkinin özelliği) → kanal kısıtı null.
        var grid = ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query, null, defaultPageSize: 50);
        return Sonuc(await mediator.Send(new GetYetkiGruplariGridQuery(
            new YetkiGrubuFiltreleri(activeOnly, sistemHaric, search), grid.Page, grid.PageSize, grid), ct));
    }

    /// <summary>Yetki gruplarını Excel'e aktarır (DataGrid).</summary>
    [HttpPost("permission-groups/export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("grid-export")]
    public async Task<IActionResult> GruplarExport(
        [FromServices] ECSPros.Api.Authorization.IAlanYetkileri alanYetkileri, [FromBody] GridExportRequest body,
        [FromServices] IConfiguration config, [FromServices] ECSPros.Iam.Application.Services.IIamDbContext iam,
        [FromServices] ILogger<YetkilendirmeController> logger, CancellationToken ct)
    {
        var filtreler = new YetkiGrubuFiltreleri(
            body.NamedValue("activeOnly") == "true", body.NamedValue("sistemHaric") == "true", body.Search);
        return await ECSPros.Api.Grid.GridExportEndpoint.RunAsync(this, body, config, iam, logger,
            "permission-groups", "yetki-gruplari", "Yetki Grupları",
            ECSPros.Api.Grid.YetkiGrubuExportColumns.All,
            max => mediator.Send(new ExportYetkiGruplariQuery(filtreler, body.ToGridRequest(null), max), ct),
            ct, alanIzinleri: await alanYetkileri.IzinlerAsync(ct));
    }

    [HttpGet("permission-groups/{id:guid}")]
    public async Task<IActionResult> GrupDetay(Guid id, CancellationToken ct)
        => Sonuc(await mediator.Send(new GetYetkiGrubuDetayQuery(id), ct));

    [HttpPost("permission-groups")]
    public async Task<IActionResult> GrupOlustur([FromBody] YetkiGrubuRequest req, CancellationToken ct)
        => Sonuc(await mediator.Send(new YetkiGrubuKaydetCommand(null, req.Ad, req.Aciklama, req.Aktif, IsleyenId), ct));

    [HttpPut("permission-groups/{id:guid}")]
    public async Task<IActionResult> GrupGuncelle(Guid id, [FromBody] YetkiGrubuRequest req, CancellationToken ct)
        => Sonuc(await mediator.Send(new YetkiGrubuKaydetCommand(id, req.Ad, req.Aciklama, req.Aktif, IsleyenId), ct));

    [HttpPut("permission-groups/{id:guid}/permissions")]
    public async Task<IActionResult> GrupYetkileri(Guid id, [FromBody] GrupYetkileriRequest req, CancellationToken ct)
    {
        var tumKanallar = await TumKanallarAsync(ct);
        return Sonuc(await mediator.Send(new GrupYetkileriKaydetCommand(
            id, req.Yetkiler.Select(y => new GrupYetkiGirdisi(
                y.PermissionId, y.ChannelIds is { Count: > 0 } ? y.ChannelIds : tumKanallar)).ToList(),
            await IsleyenKapsamiAsync(ct), IsleyenId), ct));
    }

    [HttpPost("permission-groups/{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> UyeEkle(Guid id, Guid userId, CancellationToken ct)
        => Sonuc(await mediator.Send(new GrupUyeligiDegistirCommand(id, userId, true, IsleyenId), ct));

    [HttpDelete("permission-groups/{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> UyeCikar(Guid id, Guid userId, CancellationToken ct)
        => Sonuc(await mediator.Send(new GrupUyeligiDegistirCommand(id, userId, false, IsleyenId), ct));

    // ── Süper admin (K5 / tasarım §K) ─────────────────────────────────────────
    /// <summary>Süper adminlik ver/kaldır — yalnız süper admin yapabilir; kendi bayrağını kaldıramaz;
    /// sistemde en az bir süper admin kalmalıdır. Olay ayrı denetim kaydı üretir.</summary>
    [HttpPut("users/{userId:guid}/super-admin")]
    public async Task<IActionResult> SuperAdmin(Guid userId, [FromBody] SuperAdminRequest req, CancellationToken ct)
        => Sonuc(await mediator.Send(new SuperAdminDegistirCommand(
            userId, req.Deger, User.FindFirst("sa")?.Value == "true", IsleyenId), ct));

    // ── Simülasyon (Y7, tasarım §I) ───────────────────────────────────────────
    /// <summary>
    /// "Bu kullanıcı panelde ne görüyor?" — SALT OKUNUR. Hesaba giriş (impersonation) yoktur.
    /// Ayrı yetki ister (<c>iam.permissions.simulate</c>) ve her çalıştırma denetim kaydına yazılır:
    /// aksi hâlde "kim neyi görüyor" bilgisi izsiz dolaşır.
    /// </summary>
    [HttpGet("users/{userId:guid}/simulation")]
    [RequirePermission(Permissions.IamPermissionsSimulate)]
    public async Task<IActionResult> Simulasyon(
        [FromServices] ECSPros.Iam.Application.Yetkilendirme.IYetkiDenetimi denetim,
        Guid userId, CancellationToken ct)
    {
        var sonuc = await mediator.Send(new GetKullaniciSimulasyonQuery(userId), ct);
        if (sonuc.IsFailure) return BadRequest(new { success = false, error = sonuc.Error });

        await denetim.YazAsync(new YetkiDenetimKaydi(
            "yetki.simulasyon",
            $"{sonuc.Value!.AdSoyad} kullanıcısının panel görünümünü simüle etti " +
            $"({sonuc.Value.Sayfalar.Count} sayfa, {sonuc.Value.Aksiyonlar.Count} işlem yetkisi).",
            HedefKullaniciId: userId), ct);

        // Kanal kimliklerini okunur ada çevir (ekranda kimlik gösterilmez).
        var kanalAdlari = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToDictionaryAsync(
            coreDb.FirmPlatforms.Select(fp => new { fp.Id, fp.Code, fp.NameI18n }),
            x => x.Id.ToString(),
            x => x.NameI18n.ContainsKey("tr") ? x.NameI18n["tr"] : x.Code, ct);

        var v = sonuc.Value;
        string Ad(string id) => kanalAdlari.TryGetValue(id, out var ad) ? ad : id[..8];
        return Ok(new
        {
            success = true,
            data = new
            {
                v.UserId, v.AdSoyad, v.Email, v.Aktif, v.SuperAdmin, v.Gruplar,
                Sayfalar = v.Sayfalar.Select(x => new { x.Code, x.Ad, x.Modul, x.Sayfa, x.TumKanallar, Kanallar = x.Kanallar.Select(Ad).ToList() }),
                Aksiyonlar = v.Aksiyonlar.Select(x => new { x.Code, x.Ad, x.Modul, x.Sayfa, x.TumKanallar, Kanallar = x.Kanallar.Select(Ad).ToList() }),
                v.GorunenAlanlar, v.GizliAlanlar,
                Kanallar = v.Kanallar.Select(Ad).ToList(),
                v.TumYetkiKodlari,
            },
        });
    }

    // ── Yetki Logları (tasarım §J) ────────────────────────────────────────────
    /// <summary>Yetki olayları — insan diliyle özet + önce/sonra. Kayıtlar yalnız EKLENİR.</summary>
    [HttpGet("permission-logs")]
    [RequirePermission(Permissions.IamAuditView)]
    public async Task<IActionResult> YetkiLoglari(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] Guid? actorId,
        [FromQuery] Guid? targetUserId, [FromQuery] Guid? targetGroupId, [FromQuery] string? olay,
        [FromQuery] string? search, CancellationToken ct = default)
    {
        // Yetki logu kanaldan bağımsızdır (kimin neyi değiştirdiği) → kanal kısıtı null.
        var grid = ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query, null, defaultPageSize: 50);
        return Sonuc(await mediator.Send(new GetYetkiLoglariQuery(
            from, to, actorId, targetUserId, targetGroupId, olay, search, grid.Page, grid.PageSize, grid), ct));
    }

    // ── Kullanıcı Yetkileri ───────────────────────────────────────────────────
    [HttpGet("users/{userId:guid}/permissions")]
    public async Task<IActionResult> KullaniciYetkileri(Guid userId, CancellationToken ct)
        => Sonuc(await mediator.Send(new GetKullaniciYetkileriQuery(userId), ct));

    /// <summary>Kullanıcı istisnası: ver | kaldir | yok (yok = istisnayı sil).</summary>
    [HttpPut("users/{userId:guid}/permissions/{permissionId:guid}")]
    public async Task<IActionResult> KullaniciIstisnasi(Guid userId, Guid permissionId,
        [FromBody] IstisnaRequest req, CancellationToken ct)
    {
        // Kanal verilmediyse "tüm kanallar (bugünkü liste)" kabul edilir — kapsamlı yetkide
        // boş liste hiçbir kanal demek olurdu ve "kapat" isteği sessizce etkisiz kalırdı.
        var kanallar = req.ChannelIds is { Count: > 0 } ? req.ChannelIds : await TumKanallarAsync(ct);
        return Sonuc(await mediator.Send(new KullaniciIstisnasiKaydetCommand(
            userId, permissionId, req.Mod, kanallar, await IsleyenKapsamiAsync(ct), IsleyenId), ct));
    }

    // ── Y8: geçişten çıkış (K8 adım 3-6) ──────────────────────────────────────

    /// <summary>Kurulabilir departman grubu şablonları (kurulu olanlar işaretli).</summary>
    [HttpGet("group-templates")]
    public async Task<IActionResult> GrupSablonlari(CancellationToken ct)
        => Sonuc(await mediator.Send(new GetGrupSablonlariQuery(), ct));

    /// <summary>Şablondan departman grubu kurar (yetkiler sonradan panelden düzenlenir).</summary>
    [HttpPost("group-templates/{kod}")]
    public async Task<IActionResult> SablondanGrup(string kod, CancellationToken ct)
        => Sonuc(await mediator.Send(new SablondanGrupOlusturCommand(
            kod, await TumKanallarAsync(ct), await IsleyenKapsamiAsync(ct), IsleyenId), ct));

    /// <summary>Grubu kopyalar — yetkiler taşınır, ÜYELER taşınmaz.</summary>
    [HttpPost("permission-groups/{id:guid}/copy")]
    public async Task<IActionResult> GrupKopyala(Guid id, [FromBody] GrupKopyalaRequest req, CancellationToken ct)
        => Sonuc(await mediator.Send(new GrupKopyalaCommand(
            id, req.Ad, await IsleyenKapsamiAsync(ct), IsleyenId), ct));

    /// <summary>Grubu kaldırır (K8 adım 6). Üyesi varsa reddedilir.</summary>
    [HttpDelete("permission-groups/{id:guid}")]
    public async Task<IActionResult> GrupKaldir(Guid id, CancellationToken ct)
        => Sonuc(await mediator.Send(new GrupKaldirCommand(id, IsleyenId), ct));

    /// <summary>K8 geçiş panosu: kimler hâlâ tam erişimde, geçiş grubu boşaldı mı?</summary>
    [HttpGet("transition-status")]
    public async Task<IActionResult> GecisDurumu(CancellationToken ct)
        => Sonuc(await mediator.Send(new GetGecisDurumuQuery(), ct));
}

public record YetkiGosterimRequest(string Ad, string? Aciklama, string? Sayfa, int Sira, bool Aktif, bool? KanalKapsamli);
public record YetkiGrubuRequest(string Ad, string? Aciklama, bool Aktif = true);
public record GrupYetkiSatiriRequest(Guid PermissionId, List<Guid>? ChannelIds);
public record GrupYetkileriRequest(List<GrupYetkiSatiriRequest> Yetkiler);
public record IstisnaRequest(string Mod, List<Guid>? ChannelIds);
public record SuperAdminRequest(bool Deger);
public record GrupKopyalaRequest(string Ad);
