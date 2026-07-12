using ECSPros.Order.Application.Queries.GetOrderDetail;
using ECSPros.Order.Application.Queries.GetOrders;
using ECSPros.Shared.Contracts;
using ECSPros.Storefront.Application.Commands.CreateProductReview;
using ECSPros.Storefront.Application.Commands.DeleteProductReview;
using ECSPros.Storefront.Application.Queries.GetMemberReviews;
using ECSPros.Storefront.Application.Queries.GetProductReviews;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>
/// E7: Ürün değerlendirmeleri — yorum yazma SATIN ALMA ŞARTLIDIR: üyenin teslim edilmiş
/// (delivered) sipariş kalemlerinden ürün kodu doğrulanır (kalemde kod yok; VariantId →
/// Catalog'dan koda çözülür — bu birleştirme yalnız API katmanında yapılır, modüller
/// birbirini bilmez). Yorumlar pending doğar; moderasyon /api/reviews'ta.
/// </summary>
[ApiController]
[Route("api/store/reviews")]
public class StoreReviewsController(IMediator mediator, IProductService productService) : ControllerBase
{
    private Guid MemberId => Guid.Parse(
        User.FindFirst("sub")?.Value
        ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value!);

    /// <summary>Üyenin teslim edilmiş sipariş kalemleri → ürün kodu haritası
    /// (kod → OrderItemId). Yorumlanabilirlik + kanıt kalemi buradan.</summary>
    private async Task<Dictionary<string, Guid>> TeslimEdilenKodlarAsync(CancellationToken ct)
    {
        var sonuc = new Dictionary<string, Guid>();
        var siparisler = await mediator.Send(new GetOrdersQuery("delivered", MemberId, null, 1, 50), ct);
        if (siparisler.IsFailure) return sonuc;

        var kalemler = new List<(Guid VariantId, Guid ItemId)>();
        foreach (var ozet in siparisler.Value!.Items)
        {
            var detay = await mediator.Send(new GetOrderDetailQuery(ozet.Id), ct);
            if (detay.IsSuccess)
                kalemler.AddRange(detay.Value!.Items.Select(i => (i.VariantId, i.Id)));
        }
        if (kalemler.Count == 0) return sonuc;

        var gorunumler = await productService.GetVariantDisplayAsync(
            kalemler.Select(k => k.VariantId).Distinct().ToList(), ct);
        foreach (var (variantId, itemId) in kalemler)
            if (gorunumler.TryGetValue(variantId, out var g) && !sonuc.ContainsKey(g.ProductCode))
                sonuc[g.ProductCode] = itemId;
        return sonuc;
    }

    /// <summary>Ürünün yayında yorumları (anonim erişim — ürün sayfası/değerlendirmeler).
    /// H9 additive: ratings (çoklu puan), sort (newest|oldest), search (metin).</summary>
    [HttpGet("product/{productCode}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetForProduct(
        string productCode, [FromQuery] Guid firmPlatformId, [FromQuery] int page = 1,
        [FromQuery] List<int>? ratings = null, [FromQuery] string? sort = null,
        [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetProductReviewsQuery(firmPlatformId, productCode, page, 10, ratings, sort, search), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>H9: değerlendirme istatistiği — ortalama + toplam + puan dağılımı (anonim).</summary>
    [HttpGet("product/{productCode}/summary")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProductSummary(
        string productCode, [FromQuery] Guid firmPlatformId, CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new ECSPros.Storefront.Application.Queries.GetProductReviewSummary
                .GetProductReviewSummaryQuery(firmPlatformId, productCode), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yorumlarım — üyenin tüm yorumları (sekmeler client'ta ayrışır).</summary>
    [HttpGet("mine")]
    [Authorize(Policy = "MemberOnly")]
    public async Task<IActionResult> GetMine([FromQuery] Guid firmPlatformId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetMemberReviewsQuery(firmPlatformId, MemberId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost]
    [Authorize(Policy = "MemberOnly")]
    public async Task<IActionResult> Create([FromBody] StoreReviewRequest req, CancellationToken ct)
    {
        var teslimEdilenler = await TeslimEdilenKodlarAsync(ct);
        if (!teslimEdilenler.TryGetValue(req.ProductCode?.Trim() ?? "", out var orderItemId))
            return BadRequest(new { success = false, error = "Yalnızca teslim edilmiş siparişlerinizdeki ürünleri değerlendirebilirsiniz." });

        // Yayında ad maskeli görünür: "Efe K." → "E*** K."
        var tamAd = User.FindFirst("full_name")?.Value ?? "Üye";
        var maskeli = string.Join(" ", tamAd.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Length <= 1 ? p : p[0] + new string('*', Math.Min(3, p.Length - 1))));

        var result = await mediator.Send(new CreateProductReviewCommand(
            req.FirmPlatformId, MemberId, req.ProductCode!, orderItemId, req.Rating, req.Text, maskeli), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { reviewId = result.Value } });
    }

    /// <summary>Üyenin yorumlayabileceği ürün kodları (teslim edilmiş − yorumlanmış).</summary>
    [HttpGet("reviewable")]
    [Authorize(Policy = "MemberOnly")]
    public async Task<IActionResult> GetReviewable([FromQuery] Guid firmPlatformId, CancellationToken ct)
    {
        var teslimEdilenler = await TeslimEdilenKodlarAsync(ct);
        var yorumlar = await mediator.Send(new GetMemberReviewsQuery(firmPlatformId, MemberId), ct);
        var yorumlanan = yorumlar.IsSuccess
            ? yorumlar.Value!.Where(y => !y.IsDeleted).Select(y => y.ProductCode).ToHashSet()
            : new HashSet<string>();
        return Ok(new { success = true, data = teslimEdilenler.Keys.Where(k => !yorumlanan.Contains(k)).ToList() });
    }

    [HttpDelete("{reviewId}")]
    [Authorize(Policy = "MemberOnly")]
    public async Task<IActionResult> Delete(Guid reviewId, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteProductReviewCommand(MemberId, reviewId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }
}

public record StoreReviewRequest(Guid FirmPlatformId, string? ProductCode, int Rating, string? Text);
