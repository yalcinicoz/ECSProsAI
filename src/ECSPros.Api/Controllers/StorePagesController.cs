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
public class StorePagesController(IPageComposer composer) : ControllerBase
{
    /// <summary>
    /// Yerleşimin görünür blokları (ürün/koleksiyon blokları kaynak konfigürasyonundan
    /// doldurulmuş). Yayın yoksa boş dizi. Örnek: GET /api/store/pages/homepage?firmPlatformId=...
    /// </summary>
    [HttpGet("{placement}")]
    public async Task<IActionResult> GetPlacement(
        string placement, [FromQuery] Guid firmPlatformId, CancellationToken ct)
    {
        if (!PageBlockCatalog.IsValidPlacement(placement))
            return BadRequest(new { success = false, error = "Geçersiz yerleşim." });
        if (firmPlatformId == Guid.Empty)
            return BadRequest(new { success = false, error = "firmPlatformId zorunlu." });

        var (version, blocks) = await composer.ComposeAsync(firmPlatformId, placement, ct);
        return Ok(new { success = true, data = new { version, blocks } });
    }

    /// <summary>Infinity ürün bloğunun devam sayfası. Blok aktif snapshot'ta yoksa 404.</summary>
    [HttpGet("blocks/{blockId:guid}/products")]
    public async Task<IActionResult> GetBlockProducts(
        Guid blockId, [FromQuery] Guid firmPlatformId, [FromQuery] int page = 2, CancellationToken ct = default)
    {
        if (firmPlatformId == Guid.Empty)
            return BadRequest(new { success = false, error = "firmPlatformId zorunlu." });

        var urunler = await composer.ResolveBlockProductsAsync(firmPlatformId, blockId, page, ct);
        if (urunler is null)
            return NotFound(new { success = false, error = "Blok aktif yayında bulunamadı." });
        return Ok(new { success = true, data = new { items = urunler, page } });
    }
}
