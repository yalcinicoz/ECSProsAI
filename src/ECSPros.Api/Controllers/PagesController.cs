using System.Security.Claims;
using ECSPros.Storefront.Application.Commands.DeletePageBlock;
using ECSPros.Storefront.Application.Commands.PublishPageSnapshot;
using ECSPros.Storefront.Application.Commands.ReorderPageBlocks;
using ECSPros.Storefront.Application.Commands.RollbackPageSnapshot;
using ECSPros.Storefront.Application.Commands.SavePageBlock;
using ECSPros.Storefront.Application.Commands.SavePageBlockItems;
using ECSPros.Storefront.Application.Queries.GetPageBlockDetail;
using ECSPros.Storefront.Application.Queries.GetPageBlocks;
using ECSPros.Storefront.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Controllers;

/// <summary>
/// G4/G6: vitrin yayın yönetimi (admin). Blok/öğe CRUD endpoint'leri G6'da tamamlanır;
/// Yayınla + rollback + yayın geçmişi burada — canlı okuma tarafının (G4) sözleşmesi
/// bu komutlarla üretilen snapshot'lardır.
/// </summary>
[ApiController]
[Route("api/pages")]
[Authorize]
public class PagesController(
    IMediator mediator,
    ECSPros.Storefront.Application.Services.IStorefrontDbContext db,
    ECSPros.Api.Services.Store.IVitrinAuditLogger audit) : ControllerBase
{
    public record PublishRequest(Guid FirmPlatformId, string? Note);
    public record RollbackRequest(Guid FirmPlatformId, int TargetVersion, string? Note);
    public record BlockRequest(
        Guid FirmPlatformId, string Placement, string BlockType, string? Template,
        Dictionary<string, string> TitleI18n, Dictionary<string, string>? SubtitleI18n,
        int SortOrder, bool IsActive, DateTime? StartAt, DateTime? EndAt, int Priority,
        string? RuleJson, string? ConfigJson);
    public record ReorderRequest(Guid FirmPlatformId, string Placement, List<Guid> OrderedIds);
    public record ItemsRequest(Guid FirmPlatformId, List<PageBlockItemInput> Items);

    /// <summary>Blok paleti — admin dropdown'ları PageBlockCatalog'dan beslenir (G2 tek kaynak).</summary>
    [HttpGet("catalog")]
    public IActionResult Catalog() => Ok(new
    {
        success = true,
        data = new
        {
            placements = PageBlockCatalog.Placements.Select(p => new { code = p.Code, displayName = p.DisplayName }),
            blockTypes = PageBlockCatalog.BlockTypes.Select(t => new
            {
                code = t.Code,
                displayName = t.DisplayName,
                ruleLevel = t.Rules.ToString(),
                supportsItems = t.SupportsItems,
                templates = t.Templates,
                requiresProductSource = t.RequiresProductSource,
                requiresCollectionSource = t.RequiresCollectionSource,
            }),
            carouselThemes = PageBlockCatalog.CarouselThemes,
        },
    });

    [HttpGet("blocks")]
    public async Task<IActionResult> GetBlocks(
        [FromQuery] Guid firmPlatformId, [FromQuery] string? placement, CancellationToken ct)
    {
        var result = await mediator.Send(new GetPageBlocksQuery(firmPlatformId, placement), ct);
        return Ok(new { success = true, data = result.Value });
    }

    [HttpGet("blocks/{id:guid}")]
    public async Task<IActionResult> GetBlock(Guid id, [FromQuery] Guid firmPlatformId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetPageBlockDetailQuery(id, firmPlatformId), ct);
        if (result.IsFailure) return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("blocks")]
    public async Task<IActionResult> CreateBlock([FromBody] BlockRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(BlokKomutu(null, req), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        await audit.LogAsync(HttpContext, "Created",
            ECSPros.Api.Services.Store.VitrinAuditLogger.BlockEntityType(req.BlockType),
            result.Value, null, req, req.FirmPlatformId, BaslikYaz(req.TitleI18n), ct);
        if (req.RuleJson is not null)
            await audit.LogAsync(HttpContext, "Created", "Rule", result.Value,
                null, new { ruleJson = req.RuleJson }, req.FirmPlatformId, BaslikYaz(req.TitleI18n), ct);
        return Created($"/api/pages/blocks/{result.Value}", new { success = true, data = new { id = result.Value } });
    }

    [HttpPut("blocks/{id:guid}")]
    public async Task<IActionResult> UpdateBlock(Guid id, [FromBody] BlockRequest req, CancellationToken ct)
    {
        // G13: eski değer audit için mutasyondan ÖNCE okunur (aktiflik/kural farkı da buradan)
        var eski = (await mediator.Send(new GetPageBlockDetailQuery(id, req.FirmPlatformId), ct)).Value;
        var result = await mediator.Send(BlokKomutu(id, req), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });

        var aksiyon = eski is not null && eski.IsActive != req.IsActive
            ? (req.IsActive ? "Activated" : "Deactivated") : "Updated";
        await audit.LogAsync(HttpContext, aksiyon,
            ECSPros.Api.Services.Store.VitrinAuditLogger.BlockEntityType(req.BlockType),
            id, eski, req, req.FirmPlatformId, BaslikYaz(req.TitleI18n), ct);
        if (eski is not null && eski.RuleJson != req.RuleJson)
            await audit.LogAsync(HttpContext,
                eski.RuleJson is null ? "Created" : req.RuleJson is null ? "Deleted" : "Updated",
                "Rule", id, new { ruleJson = eski.RuleJson }, new { ruleJson = req.RuleJson },
                req.FirmPlatformId, BaslikYaz(req.TitleI18n), ct);
        return Ok(new { success = true, data = new { id = result.Value } });
    }

    [HttpDelete("blocks/{id:guid}")]
    public async Task<IActionResult> DeleteBlock(Guid id, [FromQuery] Guid firmPlatformId, CancellationToken ct)
    {
        var eski = (await mediator.Send(new GetPageBlockDetailQuery(id, firmPlatformId), ct)).Value;
        var result = await mediator.Send(new DeletePageBlockCommand(id, firmPlatformId), ct);
        if (result.IsFailure) return NotFound(new { success = false, error = result.Error });
        await audit.LogAsync(HttpContext, "Deleted",
            ECSPros.Api.Services.Store.VitrinAuditLogger.BlockEntityType(eski?.BlockType ?? ""),
            id, eski, null, firmPlatformId, BaslikYaz(eski?.TitleI18n), ct);
        return Ok(new { success = true });
    }

    [HttpPut("blocks/order")]
    public async Task<IActionResult> ReorderBlocks([FromBody] ReorderRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new ReorderPageBlocksCommand(req.FirmPlatformId, req.Placement, req.OrderedIds), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        await audit.LogAsync(HttpContext, "Updated", "PagePlacement", req.FirmPlatformId,
            null, new { req.Placement, req.OrderedIds }, req.FirmPlatformId, $"Sıralama: {req.Placement}", ct);
        return Ok(new { success = true });
    }

    /// <summary>Öğe listesi replace (SaveNavNodes deseni — editör tam listeyi gönderir).</summary>
    [HttpPut("blocks/{id:guid}/items")]
    public async Task<IActionResult> SaveItems(Guid id, [FromBody] ItemsRequest req, CancellationToken ct)
    {
        var eski = (await mediator.Send(new GetPageBlockDetailQuery(id, req.FirmPlatformId), ct)).Value;
        var result = await mediator.Send(new SavePageBlockItemsCommand(id, req.FirmPlatformId, req.Items), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        await audit.LogAsync(HttpContext, "Updated",
            ECSPros.Api.Services.Store.VitrinAuditLogger.ItemEntityType(eski?.BlockType ?? ""),
            id, eski?.Items, req.Items, req.FirmPlatformId, BaslikYaz(eski?.TitleI18n), ct);
        return Ok(new { success = true });
    }

    private static string? BaslikYaz(Dictionary<string, string>? baslik) =>
        baslik is null ? null
        : baslik.TryGetValue("tr", out var tr) ? tr : baslik.Values.FirstOrDefault();

    private static SavePageBlockCommand BlokKomutu(Guid? id, BlockRequest req) => new(
        id, req.FirmPlatformId, req.Placement, req.BlockType, req.Template,
        req.TitleI18n, req.SubtitleI18n, req.SortOrder, req.IsActive,
        req.StartAt, req.EndAt, req.Priority, req.RuleJson, req.ConfigJson);

    public record PreviewRequest(
        Guid FirmPlatformId, string Placement, string? City, string? Gender,
        string? Device, bool IsMember, Guid? MemberGroupId);

    /// <summary>Vitrin öğe görseli yükleme (2026-07-22): URL elle girilmez — dosya
    /// media/vitrin altına kaydedilir, dönen /media yolu öğeye yazılır (E8 iade deseni).</summary>
    [HttpPost("media")]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> UploadMedia(
        IFormFile? file, [FromServices] IConfiguration configuration, CancellationToken ct)
    {
        var uzantilar = new Dictionary<string, string>
        {
            ["image/jpeg"] = ".jpg", ["image/png"] = ".png",
            ["image/webp"] = ".webp", ["image/gif"] = ".gif", ["image/svg+xml"] = ".svg",
        };
        if (file is null || file.Length == 0)
            return BadRequest(new { success = false, error = "Görsel dosyası gönderilmedi." });
        if (file.Length > 5_000_000)
            return BadRequest(new { success = false, error = "Görsel en fazla 5 MB olabilir." });
        if (!uzantilar.TryGetValue(file.ContentType, out var uzanti))
            return BadRequest(new { success = false, error = "Yalnızca JPEG, PNG, WebP, GIF veya SVG yükleyebilirsiniz." });

        var kok = configuration["Store:MediaRootPath"] ?? "/opt/ECSProsAI/media";
        var altDizin = Path.Combine("vitrin", DateTime.UtcNow.ToString("yyyyMM"));
        Directory.CreateDirectory(Path.Combine(kok, altDizin));
        var ad = $"{Guid.NewGuid():N}{uzanti}";
        await using (var hedef = System.IO.File.Create(Path.Combine(kok, altDizin, ad)))
            await file.CopyToAsync(hedef, ct);

        return Ok(new { success = true, data = new { url = $"/media/{altDizin.Replace(Path.DirectorySeparatorChar, '/')}/{ad}" } });
    }

    /// <summary>
    /// Canlı-önizlemeli blok editörü (2026-07-22): yerleşimin TASLAK blokları GERÇEK
    /// içerikleriyle — öğe görselleri + ürün kaynaklı bloklarda çözülmüş ürün kartları
    /// (ilk 8) + koleksiyon özetleri. Salt okuma; canlı yayına/cache'e dokunmaz.
    /// </summary>
    [HttpGet("draft-compose")]
    public async Task<IActionResult> DraftCompose(
        [FromQuery] Guid firmPlatformId, [FromQuery] string placement,
        [FromServices] ECSPros.Api.Services.Store.IPageBlockSourceResolver resolver,
        CancellationToken ct)
    {
        if (!PageBlockCatalog.IsValidPlacement(placement))
            return BadRequest(new { success = false, error = "Geçersiz yerleşim." });
        if (firmPlatformId == Guid.Empty)
            return BadRequest(new { success = false, error = "firmPlatformId zorunlu." });

        var sonuc = await mediator.Send(
            new ECSPros.Storefront.Application.Queries.GetDraftBlocksWithItems
                .GetDraftBlocksWithItemsQuery(firmPlatformId, placement), ct);
        if (sonuc.IsFailure) return BadRequest(new { success = false, error = sonuc.Error });

        var bloklar = new List<object>();
        foreach (var b in sonuc.Value!)
        {
            // Ürün kaynağı: önizleme için ilk 8 kart yeter (editör performansı)
            object? urunler = null;
            var kaynak = resolver.ParseProductSource(b.ConfigJson);
            if (kaynak is not null && !ECSPros.Api.Services.Store.PageBlockSourceResolver.UyeBaglamli(kaynak.Source))
            {
                var kartlar = await resolver.ResolveProductsAsync(
                    firmPlatformId, kaynak with { Limit = Math.Min(kaynak.Limit, 8) }, 1, ct: ct);
                urunler = kartlar.Select(k => new
                {
                    k.Code,
                    Ad = k.NameI18n.GetValueOrDefault("tr") ?? k.NameI18n.Values.FirstOrDefault(),
                    Gorsel = k.MainImageUrl,
                    Fiyat = k.MinPrice,
                }).ToList();
            }

            object? koleksiyonlar = null;
            var kKaynak = resolver.ParseCollectionSource(b.ConfigJson);
            if (kKaynak is not null)
            {
                var liste = await resolver.ResolveCollectionsAsync(
                    firmPlatformId, kKaynak with { Limit = Math.Min(kKaynak.Limit, 6) }, ct);
                koleksiyonlar = liste.Select(k => new { k.Name, k.ItemCount, k.ViewCount }).ToList();
            }

            bloklar.Add(new
            {
                b.Id, b.BlockType, b.Template, b.TitleI18n, b.SubtitleI18n,
                b.SortOrder, b.IsActive, b.StartAt, b.EndAt,
                UyeBaglamli = kaynak is not null
                    && ECSPros.Api.Services.Store.PageBlockSourceResolver.UyeBaglamli(kaynak.Source),
                KaynakTipi = kaynak?.Source,
                Items = b.Items,
                Urunler = urunler,
                Koleksiyonlar = koleksiyonlar,
            });
        }

        return Ok(new { success = true, data = bloklar });
    }

    /// <summary>
    /// G12: önizleme — TASLAK veri + kurgu segment üzerinde kural motorunu çalıştırır,
    /// blokları görünür/gizli + nedeniyle listeler (spec: canlı siteyi etkilemez, cache'e
    /// yazmaz; yayınlanmamış değişiklikler burada görünür).
    /// </summary>
    [HttpPost("preview")]
    public async Task<IActionResult> Preview(
        [FromBody] PreviewRequest req,
        [FromServices] ECSPros.Api.Services.Store.IPagePreviewService preview,
        [FromServices] ECSPros.Api.Services.Store.IVisitorSegmentResolver segmentResolver,
        CancellationToken ct)
    {
        if (!PageBlockCatalog.IsValidPlacement(req.Placement))
            return BadRequest(new { success = false, error = "Geçersiz yerleşim." });
        if (req.FirmPlatformId == Guid.Empty)
            return BadRequest(new { success = false, error = "firmPlatformId zorunlu." });

        var segment = await segmentResolver.BuildAsync(
            req.City, req.Gender, req.Device, req.IsMember, req.MemberGroupId, ct);
        var bloklar = await preview.PreviewAsync(req.FirmPlatformId, req.Placement, segment, ct);
        await audit.LogAsync(HttpContext, "Previewed", "PagePlacement", req.FirmPlatformId,
            null, new { req.Placement, segment = segment.CacheKey() }, req.FirmPlatformId,
            $"Önizleme: {req.Placement}", ct);
        return Ok(new
        {
            success = true,
            data = new
            {
                segment = new
                {
                    city = segment.CityCode,
                    cityName = segment.CityName,
                    region = segment.Region,
                    gender = segment.Gender,
                    device = segment.Device,
                    membership = segment.IsMember ? "member" : "guest",
                    memberGroup = segment.MemberGroupId,
                },
                blocks = bloklar,
            },
        });
    }

    [HttpPost("publish")]
    public async Task<IActionResult> Publish([FromBody] PublishRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new PublishPageSnapshotCommand(
            request.FirmPlatformId, KullaniciId(), request.Note), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        await audit.LogAsync(HttpContext, "Published", "PublishedSnapshot", request.FirmPlatformId,
            null, new { version = result.Value, request.Note }, request.FirmPlatformId,
            $"Yayın v{result.Value}", ct);
        return Ok(new { success = true, data = new { version = result.Value } });
    }

    [HttpPost("rollback")]
    public async Task<IActionResult> Rollback([FromBody] RollbackRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new RollbackPageSnapshotCommand(
            request.FirmPlatformId, request.TargetVersion, KullaniciId(), request.Note), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        await audit.LogAsync(HttpContext, "Rollback", "PublishedSnapshot", request.FirmPlatformId,
            null, new { request.TargetVersion, request.Note }, request.FirmPlatformId,
            $"Geri dönüş v{request.TargetVersion}", ct);
        return Ok(new { success = true });
    }

    /// <summary>
    /// G13: değişiklik geçmişi — vitrin varlıklarının audit kayıtları (iam.audit_logs;
    /// spec 'Değişiklik Geçmişi' ekranı). Platform süzgeci Context jsonb'sinden bellek
    /// tarafında (jsonb sözlük indeksi SQL'e çevrilemiyor — B2 dersi; kayıt hacmi admin
    /// işlemleriyle sınırlı, son 500 pencere yeterli).
    /// </summary>
    [HttpGet("audit-logs")]
    public IActionResult AuditLogs(
        [FromQuery] Guid firmPlatformId,
        [FromServices] ECSPros.Iam.Application.Services.IIamDbContext iam,
        [FromQuery] int limit = 100)
    {
        var vitrinTipleri = ECSPros.Api.Services.Store.VitrinAuditLogger.EntityTypes;
        var kayitlar = iam.AuditLogs.AsNoTracking()
            .Where(l => vitrinTipleri.Contains(l.EntityType))
            .OrderByDescending(l => l.CreatedAt)
            .Take(500)
            .ToList()
            .Where(l => l.Context != null
                && l.Context.TryGetValue("firmPlatformId", out var p)
                && p?.ToString() == firmPlatformId.ToString())
            .Take(Math.Clamp(limit, 1, 200))
            .Select(l => new
            {
                l.Id,
                action = l.Action,
                l.EntityType,
                l.EntityId,
                l.CreatedAt,
                userName = l.Context!.TryGetValue("userName", out var u) ? u?.ToString() : null,
                title = l.Context!.TryGetValue("title", out var t) ? t?.ToString() : null,
                oldValues = l.OldValues,
                newValues = l.NewValues,
            });
        return Ok(new { success = true, data = kayitlar });
    }

    /// <summary>Yayın geçmişi — spec PublishLog listesi (yeniden eskiye).</summary>
    [HttpGet("publish-logs")]
    public IActionResult PublishLogs([FromQuery] Guid firmPlatformId, [FromQuery] int limit = 50)
    {
        var logs = db.PublishLogs
            .Where(l => l.FirmPlatformId == firmPlatformId)
            .OrderByDescending(l => l.PublishedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .Select(l => new { l.Id, l.Version, l.PreviousVersion, l.PublishedBy, l.PublishedAt, l.Status, l.ErrorMessage, l.Note })
            .ToList();
        return Ok(new { success = true, data = logs });
    }

    /// <summary>Snapshot versiyonları (rollback seçim listesi) — JsonData listede taşınmaz.</summary>
    [HttpGet("snapshots")]
    public IActionResult Snapshots([FromQuery] Guid firmPlatformId, [FromQuery] int limit = 50)
    {
        var versiyonlar = db.PublishedSnapshots
            .Where(s => s.FirmPlatformId == firmPlatformId)
            .OrderByDescending(s => s.Version)
            .Take(Math.Clamp(limit, 1, 200))
            .Select(s => new { s.Id, s.Version, s.PublishedAt, s.PublishedBy, s.IsActive, s.Status, s.Note })
            .ToList();
        return Ok(new { success = true, data = versiyonlar });
    }

    private Guid? KullaniciId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
            ? id : null;
}
