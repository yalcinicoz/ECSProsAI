using ECSPros.Order.Application.Queries.GetMemberDeliveredItems;
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

    /// <summary>Üyenin teslim edilmiş sipariş kalemleri, ürün koduna çözülmüş ve ürün başına TEK kayıt
    /// (en yeni sipariş). Yorumlanabilirlik + satın alma kanıtı buradan. A11 (2026-09-07): tek toplu sorgu
    /// (eski N+1 sipariş detayı yerine) ve ad/görsel/varyant özeti ile zengin liste.</summary>
    private async Task<List<ReviewableProductDto>> TeslimEdilenUrunlerAsync(CancellationToken ct)
    {
        var kalemler = await mediator.Send(new GetMemberDeliveredItemsQuery(MemberId, 50), ct);
        if (kalemler.IsFailure || kalemler.Value!.Count == 0) return [];

        var gorunumler = await productService.GetVariantDisplayAsync(
            kalemler.Value.Select(k => k.VariantId).Distinct().ToList(), ct);

        var sonuc = new List<ReviewableProductDto>();
        var gorulen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var k in kalemler.Value) // sipariş tarihine göre yeniden eskiye
        {
            if (!gorunumler.TryGetValue(k.VariantId, out var g) || !gorulen.Add(g.ProductCode)) continue;
            var ad = g.ProductNameI18n.TryGetValue("tr", out var tr) ? tr
                   : g.ProductNameI18n.Values.FirstOrDefault() ?? k.ProductName;
            sonuc.Add(new ReviewableProductDto(
                g.ProductCode, ad, g.ImageUrl, g.OptionsText ?? k.VariantInfo,
                k.OrderId, k.OrderNumber, k.OrderedAt, k.OrderItemId, k.VariantId, g.ColorValueId, g.SizeValueId));
        }
        return sonuc;
    }

    private async Task<Dictionary<string, Guid>> TeslimEdilenKodlarAsync(CancellationToken ct) =>
        (await TeslimEdilenUrunlerAsync(ct)).ToDictionary(u => u.ProductCode, u => u.OrderItemId, StringComparer.Ordinal);

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

    /// <summary>Yorumlarım — üyenin tüm yorumları (sekmeler client'ta ayrışır). B1 (2026-09-07): satırda
    /// ürün adı + görsel de gelir (productName/imageUrl; ürün satışta değilse null).</summary>
    [HttpGet("mine")]
    [Authorize(Policy = "MemberOnly")]
    public async Task<IActionResult> GetMine(
        [FromQuery] Guid firmPlatformId,
        [FromServices] ECSPros.Api.Services.Store.StoreKartZenginlestirici zengin = null!, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetMemberReviewsQuery(firmPlatformId, MemberId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        var harita = await zengin.GetirAsync(firmPlatformId, result.Value!.Select(y => y.ProductCode), ct);
        var data = result.Value.Select(y =>
        {
            var o = ECSPros.Api.Services.Store.StoreKartZenginlestirici.Ozet(harita, y.ProductCode);
            return new ECSPros.Api.Models.Store.MemberReviewItemDto(y.Id, y.ProductCode, y.Rating, y.Text, y.Status,
                y.RejectReason, y.IsDeleted, y.CreatedAt, y.DeletedAt, y.Topic, y.Photos, o?.ProductName, o?.ImageUrl);
        }).ToList();
        return Ok(new { success = true, data });
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
        [FromForm] List<IFormFile> files,
        [FromServices] ECSPros.Api.Services.Storage.IFileStorage storage,
        CancellationToken ct)
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

        var altDizin = $"reviews/{DateTime.UtcNow:yyyyMM}";

        var urls = new List<string>();
        foreach (var dosya in files)
        {
            var ad = $"{Guid.NewGuid():N}{uzantilar[dosya.ContentType]}";
            await using var stream = dosya.OpenReadStream();
            var stored = await storage.SavePublicAsync(altDizin, ad, stream, dosya.ContentType, ct);
            urls.Add(stored.PublicUrl);
        }

        return Ok(new { success = true, data = new { urls } });
    }

    /// <summary>Üyenin yorumlayabileceği ürünler (teslim edilmiş − yorumlanmış). A11 (2026-09-07): düz kod
    /// listesi yerine ad/görsel/varyant özeti/sipariş no içeren nesne listesi — istemci katalog tamamlamaz.</summary>
    [HttpGet("reviewable")]
    [Authorize(Policy = "MemberOnly")]
    public async Task<IActionResult> GetReviewable([FromQuery] Guid firmPlatformId, CancellationToken ct)
    {
        var teslimEdilenler = await TeslimEdilenUrunlerAsync(ct);
        var yorumlar = await mediator.Send(new GetMemberReviewsQuery(firmPlatformId, MemberId), ct);
        var yorumlanan = yorumlar.IsSuccess
            ? yorumlar.Value!.Where(y => !y.IsDeleted).Select(y => y.ProductCode).ToHashSet()
            : new HashSet<string>();
        return Ok(new { success = true, data = teslimEdilenler.Where(u => !yorumlanan.Contains(u.ProductCode)).ToList() });
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

/// <summary>A11: yorumlanabilir ürün — mobil/web listesi ek istek atmadan gösterir.</summary>
public record ReviewableProductDto(
    string ProductCode,
    string ProductName,
    string? ImageUrl,
    string? VariantInfo,
    Guid OrderId,
    string OrderNumber,
    DateTime OrderedAt,
    Guid OrderItemId,
    Guid VariantId,
    Guid? ColorValueId = null,
    Guid? SizeValueId = null);

public record StoreReviewRequest(
    Guid FirmPlatformId, string? ProductCode, int Rating, string? Text,
    string? Topic = null, List<string>? PhotoUrls = null); // İP-5
