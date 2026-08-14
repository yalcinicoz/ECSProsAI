using System.Text.Json;
using System.Text.Json.Serialization;
using ECSPros.Api.Services;
using ECSPros.Catalog.Application.Queries.GetVisualSearchCards;
using ECSPros.Integration.Application.Queries.ResolveErpProductRefs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ECSPros.Api.Controllers.Store;

/// <summary>
/// H3 görsel arama — legacy GorselAramaController portu. Dış servise (X-API-Key, ayarlar
/// DB'den: visual_search entegrasyonu) görseli iletir; dönen legacy ürün Id'lerini
/// legacy-MySQL YERİNE ECSPros kataloğuyla zenginleştirir: urunId → erp_variant_data
/// (erpProductId→modelCode eşlemesi) → katalog kartı (görsel/ad/fiyat/URL, liste kalıbı).
/// Yanıt şekli modal script'inin beklediği sözleşmedir (results[].imageUrl/productName/
/// modelCode/price/productUrl).
/// </summary>
[AllowAnonymous]
public class GorselAramaController(
    IMediator mediator,
    IStoreContext storeContext,
    IVisualSearchSettingsProvider settingsProvider,
    IHttpClientFactory httpClientFactory,
    ILogger<GorselAramaController> logger) : ControllerBase
{
    private const int MaxDosyaBoyutu = 10 * 1024 * 1024; // 10 MB

    [HttpPost("gorsel-arama")]
    [EnableRateLimiting("store-sensitive")] // ücretli dış servise gider — maliyet istismarına fren (2026-07-23)
    [RequestSizeLimit(MaxDosyaBoyutu + 1024)]
    public async Task<IActionResult> Ara(IFormFile? file, [FromForm] string? url, CancellationToken ct)
    {
        // 2026-08-14: URL ile arama — istemci dış görseli CORS nedeniyle indiremez;
        // bağlantı sunucuda indirilip aynı akışla dış servise iletilir.
        byte[]? urlBaytlari = null;
        string? urlDosyaAdi = null;
        string? urlIcerikTipi = null;
        if ((file is null || file.Length == 0) && !string.IsNullOrWhiteSpace(url))
        {
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var gorselUri)
                || (gorselUri.Scheme != Uri.UriSchemeHttp && gorselUri.Scheme != Uri.UriSchemeHttps))
                return BadRequest(new { error = "Geçerli bir görsel bağlantısı (http/https) girin." });

            try
            {
                var indirmeClient = httpClientFactory.CreateClient("visual-search");
                using var indirme = await indirmeClient.GetAsync(gorselUri,
                    HttpCompletionOption.ResponseHeadersRead, ct);
                if (!indirme.IsSuccessStatusCode)
                    return BadRequest(new { error = "Bağlantıdaki görsel indirilemedi." });
                if (indirme.Content.Headers.ContentLength is > MaxDosyaBoyutu)
                    return BadRequest(new { error = "Görsel 10 MB'den büyük olamaz." });

                urlBaytlari = await indirme.Content.ReadAsByteArrayAsync(ct);
                if (urlBaytlari.Length == 0)
                    return BadRequest(new { error = "Bağlantıdaki görsel indirilemedi." });
                if (urlBaytlari.Length > MaxDosyaBoyutu)
                    return BadRequest(new { error = "Görsel 10 MB'den büyük olamaz." });

                urlIcerikTipi = indirme.Content.Headers.ContentType?.MediaType;
                if (urlIcerikTipi is { Length: > 0 } && !urlIcerikTipi.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                    && urlIcerikTipi != "application/octet-stream")
                    return BadRequest(new { error = "Bağlantı bir görsele işaret etmiyor." });

                urlDosyaAdi = Path.GetFileName(gorselUri.AbsolutePath);
                if (string.IsNullOrWhiteSpace(urlDosyaAdi)) urlDosyaAdi = "url-gorseli.jpg";
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Görsel arama URL indirmesi başarısız: {Url}", url);
                return BadRequest(new { error = "Bağlantıdaki görsel indirilemedi." });
            }
        }

        if ((file is null || file.Length == 0) && urlBaytlari is null)
            return BadRequest(new { error = "Görsel dosyası gönderilmedi." });
        if (file is { Length: > MaxDosyaBoyutu })
            return BadRequest(new { error = "Görsel 10 MB'den büyük olamaz." });

        var platform = await storeContext.GetPlatformAsync(ct);

        var ayarlar = await settingsProvider.GetAsync(platform?.Id, ct);
        if (ayarlar is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Görsel arama servisi yapılandırılmamış." });

        // 1) Görseli dış servise ilet (dosya veya URL'den indirilen baytlar)
        GorselAramaServisSonucu? servisSonucu;
        try
        {
            await using var dosyaAkisi = urlBaytlari is null
                ? file!.OpenReadStream()
                : new MemoryStream(urlBaytlari);
            using var form = new MultipartFormDataContent();
            using var dosyaIcerigi = new StreamContent(dosyaAkisi);
            var icerikTipi = urlBaytlari is null ? file!.ContentType : urlIcerikTipi;
            if (!string.IsNullOrWhiteSpace(icerikTipi) && icerikTipi != "application/octet-stream")
                dosyaIcerigi.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(icerikTipi);
            form.Add(dosyaIcerigi, "file", urlBaytlari is null ? file!.FileName : urlDosyaAdi!);

            using var istek = new HttpRequestMessage(HttpMethod.Post, ayarlar.ApiUrl) { Content = form };
            istek.Headers.Add("X-API-Key", ayarlar.ApiKey);

            var httpClient = httpClientFactory.CreateClient("visual-search");
            using var cevap = await httpClient.SendAsync(istek, ct);
            var cevapMetni = await cevap.Content.ReadAsStringAsync(ct);

            if (!cevap.IsSuccessStatusCode)
            {
                logger.LogWarning("Görsel arama servisi {Status} döndü: {Body}",
                    (int)cevap.StatusCode, cevapMetni[..Math.Min(cevapMetni.Length, 300)]);
                return StatusCode(StatusCodes.Status502BadGateway,
                    new { error = "Görsel arama servisi şu anda yanıt veremiyor." });
            }

            servisSonucu = JsonSerializer.Deserialize<GorselAramaServisSonucu>(cevapMetni,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Görsel arama servisi çağrısı başarısız.");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "Görsel arama servisine ulaşılamadı." });
        }

        var servisUrunleri = servisSonucu?.Results?
            .Where(s => s.UrunId > 0)
            .GroupBy(s => s.UrunId)
            .Select(g => g.First())
            .ToList() ?? [];

        // 2) Legacy Id → modelCode (erp_variant_data), 3) modelCode → katalog kartları
        // (2026-08-15: model başına TÜM renk kartları döner — eşleşen renk ilk sırada)
        var kartlar = Enumerable.Empty<VisualSearchCardDto>().ToLookup(k => k.ModelCode);
        var modelKodlari = new Dictionary<int, string>();
        if (servisUrunleri.Count > 0)
        {
            var refSonuc = await mediator.Send(
                new ResolveErpProductRefsQuery(servisUrunleri.Select(s => s.UrunId).ToList()), ct);
            if (refSonuc.IsSuccess)
            {
                modelKodlari = refSonuc.Value!
                    .GroupBy(r => r.ErpProductId)
                    .ToDictionary(g => g.Key, g => g.First().ModelCode);

                if (modelKodlari.Count > 0 && platform is not null)
                {
                    // Eşleşen renk (2026-08-15): dış servisin bulduğu VARYANTIN barkodu —
                    // kart o rengin görseli ve ?color= linkiyle döner (siyahla arandıysa siyah).
                    var eslesenBarkodlar = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var s in servisUrunleri)
                    {
                        var satir = refSonuc.Value!.FirstOrDefault(r =>
                            r.ErpProductId == s.UrunId && r.ErpVariantId == s.UrunAnaVaryantId);
                        if (satir?.Barcode is { Length: > 0 } barkod
                            && modelKodlari.TryGetValue(s.UrunId, out var modelKodu))
                            eslesenBarkodlar[modelKodu] = barkod;
                    }

                    var kartSonuc = await mediator.Send(new GetVisualSearchCardsQuery(
                        platform.Id, modelKodlari.Values.Distinct().ToList(), eslesenBarkodlar), ct);
                    if (kartSonuc.IsSuccess)
                        kartlar = kartSonuc.Value!.ToLookup(k => k.ModelCode);
                }
            }
        }

        var sonucListe = servisUrunleri
            .SelectMany(s =>
            {
                var kartListesi = modelKodlari.TryGetValue(s.UrunId, out var kod)
                    ? kartlar[kod] : Enumerable.Empty<VisualSearchCardDto>();
                // Model başına tüm renk kartları — benzerlik sırası korunur, renkler art arda
                return kartListesi.Select(kart => new
                {
                    urunId = s.UrunId,
                    urunAnaVaryantId = s.UrunAnaVaryantId,
                    score = s.Score,
                    match = s.Match,
                    imageUrl = kart.ImageUrl,
                    productName = kart.Name.GetValueOrDefault("tr") ?? kart.Name.Values.FirstOrDefault(),
                    modelCode = kart.ModelCode,
                    price = (decimal?)kart.Price,
                    productUrl = kart.Url
                });
            })
            // Katalogda karşılığı olmayan (ör. satışa kapalı) sonuçlar liste dışı — legacy davranışı
            .Where(s => !string.IsNullOrWhiteSpace(s.imageUrl))
            .ToList();

        return new JsonResult(new
        {
            tenant = servisSonucu?.Tenant,
            count = sonucListe.Count,
            took_ms = servisSonucu?.TookMs,
            results = sonucListe
        });
    }

    private sealed class GorselAramaServisSonucu
    {
        [JsonPropertyName("tenant")] public string? Tenant { get; set; }
        [JsonPropertyName("count")] public int Count { get; set; }
        [JsonPropertyName("took_ms")] public decimal TookMs { get; set; }
        [JsonPropertyName("results")] public List<GorselAramaServisUrunu>? Results { get; set; }
    }

    private sealed class GorselAramaServisUrunu
    {
        [JsonPropertyName("urunId")] public int UrunId { get; set; }
        [JsonPropertyName("urunAnaVaryantId")] public long UrunAnaVaryantId { get; set; }
        [JsonPropertyName("score")] public decimal Score { get; set; }
        [JsonPropertyName("match")] public string? Match { get; set; }
    }
}
