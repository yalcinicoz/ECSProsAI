using System.Security.Claims;
using ECSPros.Api.Services.Marketplace.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>
/// Pazaryeri eşleme ekranları (admin): ürün grubu → pazaryeri kategorisi (birebir/koşullu/havuz),
/// özellik ve değer eşlemeleri, öneri katmanı, gözden geçirme kuyruğu.
/// Bizim taraf definition.product_groups + definition.attribute_* (yalnız okunur);
/// eşleme kayıtları integration şemasında; pazaryeri referansı marketplace_ref DB'sinden okunur.
/// </summary>
[ApiController]
[Route("api/marketplaces/mapping")]
[Authorize]
public class MarketplaceMappingController(
    MarketplaceMappingService service,
    MappingHealthService health,
    MarketplaceReadinessService readiness,
    MarketplaceCompletionService completion) : ControllerBase
{
    private Guid? UserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
            ? id : null;

    private static string Norm(string s) => s.Trim().ToLowerInvariant();

    /// <summary>Kategori sekmesinin tek çağrısı: tüm gruplar + eşleme durumları + sayaçlar.</summary>
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview([FromQuery] string marketplace, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(marketplace))
            return BadRequest(new { success = false, error = "marketplace zorunlu." });
        var data = await service.GetOverviewAsync(Norm(marketplace), ct);
        return Ok(new { success = true, data });
    }

    /// <summary>Hedef kategori seçicinin arama ucu (yalnız yaprak, aktif kategoriler).</summary>
    [HttpGet("mp-categories")]
    public async Task<IActionResult> SearchMpCategories(
        [FromQuery] string marketplace, [FromQuery] string? q, [FromQuery] int limit = 30, CancellationToken ct = default)
    {
        var data = await service.SearchMpCategoriesAsync(Norm(marketplace), q?.Trim() ?? "", Math.Clamp(limit, 1, 100), ct);
        return Ok(new { success = true, data });
    }

    /// <summary>Grup için isim benzerliğiyle kategori önerileri.</summary>
    [HttpGet("suggest-categories")]
    public async Task<IActionResult> SuggestCategories(
        [FromQuery] string marketplace, [FromQuery] Guid productGroupId, CancellationToken ct)
    {
        var data = await service.SuggestCategoriesAsync(Norm(marketplace), productGroupId, ct);
        return Ok(new { success = true, data });
    }

    [HttpPut("category")]
    public async Task<IActionResult> SaveCategoryMapping([FromBody] SaveCategoryMappingRequest request, CancellationToken ct)
    {
        var (dto, error) = await service.SaveCategoryMappingAsync(
            request with { Marketplace = Norm(request.Marketplace) }, UserId, ct);
        if (error is not null) return BadRequest(new { success = false, error });
        return Ok(new { success = true, data = dto });
    }

    [HttpDelete("category/{id:guid}")]
    public async Task<IActionResult> DeleteCategoryMapping(Guid id, CancellationToken ct)
    {
        if (!await service.DeleteCategoryMappingAsync(id, UserId, ct))
            return BadRequest(new { success = false, error = "Eşleme bulunamadı." });
        return Ok(new { success = true, data = true });
    }

    /// <summary>Özellik sekmesinin bağlam listesi: eşlemelerde hedef olan pazaryeri kategorileri.</summary>
    [HttpGet("mapped-targets")]
    public async Task<IActionResult> GetMappedTargets([FromQuery] string marketplace, CancellationToken ct)
    {
        var data = await service.GetMappedTargetsAsync(Norm(marketplace), ct);
        return Ok(new { success = true, data });
    }

    /// <summary>Pazaryeri kategorisinin özellikleri + mevcut eşlemeler + değer ilerlemesi.</summary>
    [HttpGet("attributes")]
    public async Task<IActionResult> GetAttributes(
        [FromQuery] string marketplace, [FromQuery] string mpCategoryId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(mpCategoryId))
            return BadRequest(new { success = false, error = "mpCategoryId zorunlu." });
        var data = await service.GetAttributesAsync(Norm(marketplace), mpCategoryId, ct);
        return Ok(new { success = true, data });
    }

    [HttpPut("attribute")]
    public async Task<IActionResult> SaveAttributeMapping([FromBody] SaveAttributeMappingRequest request, CancellationToken ct)
    {
        var error = await service.SaveAttributeMappingAsync(
            request with { Marketplace = Norm(request.Marketplace) }, UserId, ct);
        if (error is not null) return BadRequest(new { success = false, error });
        return Ok(new { success = true, data = true });
    }

    /// <summary>Değer eşleme paneli: bizim değerler + pazaryeri değerleri + öneriler.</summary>
    [HttpGet("values")]
    public async Task<IActionResult> GetValues(
        [FromQuery] string marketplace, [FromQuery] string mpCategoryId, [FromQuery] string mpAttributeId,
        CancellationToken ct)
    {
        var (dto, error) = await service.GetValuesAsync(Norm(marketplace), mpCategoryId, mpAttributeId, ct);
        if (error is not null) return BadRequest(new { success = false, error });
        return Ok(new { success = true, data = dto });
    }

    /// <summary>Değer eşlemelerini toplu kaydeder (hedefi boşaltılan satır eşlemesini siler).</summary>
    [HttpPut("values")]
    public async Task<IActionResult> SaveValueMappings([FromBody] SaveValueMappingsRequest request, CancellationToken ct)
    {
        var changed = await service.SaveValueMappingsAsync(
            request with { Marketplace = Norm(request.Marketplace) }, UserId, ct);
        return Ok(new { success = true, data = new { changed } });
    }

    /// <summary>Gözden geçirme kuyruğu: durumu active olmayan tüm eşlemeler.</summary>
    [HttpGet("review")]
    public async Task<IActionResult> GetReview([FromQuery] string? marketplace, CancellationToken ct)
    {
        var data = await service.GetReviewAsync(
            string.IsNullOrWhiteSpace(marketplace) ? null : Norm(marketplace), ct);
        return Ok(new { success = true, data });
    }

    public record AcknowledgeRequest(string MappingType);

    /// <summary>Gözden geçirme satırını onayla — eşleme durumunu active'e çeker.</summary>
    [HttpPost("review/{id:guid}/acknowledge")]
    public async Task<IActionResult> Acknowledge(Guid id, [FromBody] AcknowledgeRequest request, CancellationToken ct)
    {
        if (!await service.AcknowledgeAsync(request.MappingType, id, UserId, ct))
            return BadRequest(new { success = false, error = "Kayıt bulunamadı veya tür geçersiz." });
        return Ok(new { success = true, data = true });
    }

    // ── Readiness + tamamlama (F3) ──────────────────────────────────────────

    /// <summary>Yükleme hazırlık denetimini yeniden hesaplar (tüm katalog × pazaryeri).</summary>
    [HttpPost("readiness/recompute")]
    public async Task<IActionResult> RecomputeReadiness(
        [FromQuery] string marketplace, [FromBody] RecomputeReadinessRequest? req = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(marketplace))
            return BadRequest(new { success = false, error = "marketplace zorunlu." });
        // F3: gövdede productIds verilirse yalnız o ürünler yeniden hesaplanır (çekmecedeki "Hazırlığı yeniden hesapla").
        var result = await readiness.RecomputeAsync(Norm(marketplace),
            req?.ProductIds is { Count: > 0 } ? req.ProductIds : null, ct);
        return Ok(new { success = true, data = result });
    }

    /// <summary>Tamamlama ekranı verisi: nedenler + kategori adayları + eksik özellik formu.</summary>
    [HttpGet("completion")]
    public async Task<IActionResult> GetCompletion(
        [FromQuery] string marketplace, [FromQuery] Guid productId, CancellationToken ct)
    {
        var (dto, error) = await completion.GetAsync(Norm(marketplace), productId, ct);
        if (error is not null) return BadRequest(new { success = false, error });
        return Ok(new { success = true, data = dto });
    }

    /// <summary>Tamamlama kaydı (tekil veya toplu): kategori ataması istisnaya, özellik
    /// değerleri ürün-özel pazaryeri değerlerine yazılır; ürünler anında yeniden denetlenir.</summary>
    [HttpPut("completion")]
    public async Task<IActionResult> SaveCompletion([FromBody] SaveCompletionRequest request, CancellationToken ct)
    {
        var (result, error) = await completion.SaveAsync(
            request with { Marketplace = Norm(request.Marketplace) }, UserId, ct);
        if (error is not null) return BadRequest(new { success = false, error });
        return Ok(new { success = true, data = result });
    }

    /// <summary>Sağlık taramasını elle tetikler (senkron sonrası otomatik da çalışır).</summary>
    [HttpPost("health/process")]
    public async Task<IActionResult> ProcessHealth([FromQuery] string marketplace, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(marketplace))
            return BadRequest(new { success = false, error = "marketplace zorunlu." });
        var result = await health.ProcessAsync(Norm(marketplace), ct);
        return Ok(new { success = true, data = result });
    }
}

public record RecomputeReadinessRequest(List<Guid>? ProductIds);
