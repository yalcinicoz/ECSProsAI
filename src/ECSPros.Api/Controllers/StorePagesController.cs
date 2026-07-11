using ECSPros.Api.Services.Store;
using ECSPros.Storefront.Domain;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>
/// G4: vitrin yerleşim API'si — Razor'ın kullandığı kompozisyonun aynısını dışarı verir
/// (plan 3.4: mobil uygulama da bu endpoint'i kullanır). Canlı taraf yalnız aktif
/// yayınlanmış snapshot'ı okur; taslak veriye buradan ulaşılamaz.
/// </summary>
[ApiController]
[Route("api/store/pages")]
public class StorePagesController(IPageComposer composer, IVisitorSegmentResolver segmentResolver) : ControllerBase
{
    /// <summary>
    /// Yerleşimin görünür blokları (ürün/koleksiyon blokları kaynak konfigürasyonundan
    /// doldurulmuş). Yayın yoksa boş dizi. G10: kompozisyon ziyaretçi segmentine göre —
    /// anonim çalışır, geçerli üye bearer'ı segmenti üye bağlamıyla zenginleştirir
    /// (mobil app aynı endpoint'i header'larıyla kullanır).
    /// Örnek: GET /api/store/pages/homepage?firmPlatformId=...
    /// </summary>
    [HttpGet("{placement}")]
    public async Task<IActionResult> GetPlacement(
        string placement, [FromQuery] Guid firmPlatformId, CancellationToken ct)
    {
        if (!PageBlockCatalog.IsValidPlacement(placement))
            return BadRequest(new { success = false, error = "Geçersiz yerleşim." });
        if (firmPlatformId == Guid.Empty)
            return BadRequest(new { success = false, error = "firmPlatformId zorunlu." });

        var segment = await segmentResolver.ResolveAsync(
            HttpContext, VisitorSegmentResolver.MemberIdFromClaims(User), ct);
        var (version, blocks) = await composer.ComposeAsync(firmPlatformId, placement, segment, ct);
        return Ok(new { success = true, data = new { version, blocks } });
    }

    /// <summary>Infinity ürün bloğunun devam sayfası. Blok aktif snapshot'ta yoksa ya da
    /// kuraldan bu segmente görünmüyorsa 404 (içerik sızmaz).</summary>
    [HttpGet("blocks/{blockId:guid}/products")]
    public async Task<IActionResult> GetBlockProducts(
        Guid blockId, [FromQuery] Guid firmPlatformId, [FromQuery] int page = 2, CancellationToken ct = default)
    {
        if (firmPlatformId == Guid.Empty)
            return BadRequest(new { success = false, error = "firmPlatformId zorunlu." });

        var segment = await segmentResolver.ResolveAsync(
            HttpContext, VisitorSegmentResolver.MemberIdFromClaims(User), ct);
        var urunler = await composer.ResolveBlockProductsAsync(firmPlatformId, blockId, page, segment, ct);
        if (urunler is null)
            return NotFound(new { success = false, error = "Blok aktif yayında bulunamadı." });
        return Ok(new { success = true, data = new { items = urunler, page } });
    }
}
