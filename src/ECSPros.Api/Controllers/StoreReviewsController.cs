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
        [FromQuery] string? search = null, [FromQuery] List<string>? topics = null,
        [FromQuery] bool photosOnly = false, CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetProductReviewsQuery(firmPlatformId, productCode, page, 10, ratings, sort, search,
                topics, photosOnly), ct);
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

        // İP-5: yalnız kendi upload endpoint'imizin ürettiği /media/reviews/ URL'leri kabul edilir.
        var fotolar = (req.PhotoUrls ?? [])
            .Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()).Distinct().ToList();
        if (fotolar.Count > 5)
            return BadRequest(new { success = false, error = "En fazla 5 fotoğraf ekleyebilirsiniz." });
        if (fotolar.Any(u => !u.StartsWith("/media/reviews/", StringComparison.Ordinal) || u.Contains("..")))
            return BadRequest(new { success = false, error = "Geçersiz fotoğraf adresi." });

        var result = await mediator.Send(new CreateProductReviewCommand(
            req.FirmPlatformId, MemberId, req.ProductCode!, orderItemId, req.Rating, req.Text, maskeli,
            req.Topic, fotolar), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { reviewId = result.Value } });
    }

    /// <summary>İP-5: yorum fotoğrafları — /media/reviews altına yazılır (nginx sunar).
    /// E8 iade görseli kalıbının kopyası: en çok 5 dosya × 5 MB; uzantı içerik tipinden.</summary>
    [HttpPost("images")]
    [Authorize(Policy = "MemberOnly")]
    [RequestSizeLimit(30_000_000)]
    public async Task<IActionResult> UploadReviewImages(
        [FromForm] List<IFormFile> files, [FromServices] IConfiguration configuration, CancellationToken ct)
    {
        var uzantilar = new Dictionary<string, string>
        {
            ["image/jpeg"] = ".jpg", ["image/png"] = ".png", ["image/webp"] = ".webp", ["image/gif"] = ".gif"
        };

        if (files.Count == 0)
            return BadRequest(new { success = false, error = "Yüklenecek fotoğraf bulunamadı." });
        if (files.Count > 5)
            return BadRequest(new { success = false, error = "En fazla 5 fotoğraf yükleyebilirsiniz." });
        if (files.Any(f => f.Length > 5_000_000))
            return BadRequest(new { success = false, error = "Her fotoğraf en fazla 5 MB olabilir." });
        if (files.Any(f => !uzantilar.ContainsKey(f.ContentType)))
            return BadRequest(new { success = false, error = "Yalnızca JPEG, PNG, WebP veya GIF yükleyebilirsiniz." });

        var kok = configuration["Store:MediaRootPath"] ?? "/opt/ECSProsAI/media";
        var altDizin = Path.Combine("reviews", DateTime.UtcNow.ToString("yyyyMM"));
        Directory.CreateDirectory(Path.Combine(kok, altDizin));

        var urls = new List<string>();
        foreach (var dosya in files)
        {
            var ad = $"{Guid.NewGuid():N}{uzantilar[dosya.ContentType]}";
            await using var hedef = System.IO.File.Create(Path.Combine(kok, altDizin, ad));
            await dosya.CopyToAsync(hedef, ct);
            urls.Add($"/media/{altDizin.Replace(Path.DirectorySeparatorChar, '/')}/{ad}");
        }

        return Ok(new { success = true, data = new { urls } });
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

public record StoreReviewRequest(
    Guid FirmPlatformId, string? ProductCode, int Rating, string? Text,
    string? Topic = null, List<string>? PhotoUrls = null); // İP-5
