using System.Text.Json;
using System.Text.Json.Serialization;
using ECSPros.Api.Services;
using ECSPros.Catalog.Application.Queries.GetVisualSearchCards;
using ECSPros.Integration.Application.Queries.ResolveErpProductRefs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    [RequestSizeLimit(MaxDosyaBoyutu + 1024)]
    public async Task<IActionResult> Ara(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Görsel dosyası gönderilmedi." });
        if (file.Length > MaxDosyaBoyutu)
            return BadRequest(new { error = "Görsel 10 MB'den büyük olamaz." });

        var platform = await storeContext.GetPlatformAsync(ct);

        var ayarlar = await settingsProvider.GetAsync(platform?.Id, ct);
        if (ayarlar is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Görsel arama servisi yapılandırılmamış." });

        // 1) Görseli dış servise ilet
        GorselAramaServisSonucu? servisSonucu;
        try
        {
            await using var dosyaAkisi = file.OpenReadStream();
            using var form = new MultipartFormDataContent();
            using var dosyaIcerigi = new StreamContent(dosyaAkisi);
            if (!string.IsNullOrWhiteSpace(file.ContentType))
                dosyaIcerigi.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            form.Add(dosyaIcerigi, "file", file.FileName);

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

        // 2) Legacy Id → modelCode (erp_variant_data), 3) modelCode → katalog kartı
        var kartlar = new Dictionary<string, VisualSearchCardDto>();
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
                    var kartSonuc = await mediator.Send(new GetVisualSearchCardsQuery(
                        platform.Id, modelKodlari.Values.Distinct().ToList()), ct);
                    if (kartSonuc.IsSuccess)
                        kartlar = kartSonuc.Value!.ToDictionary(k => k.ModelCode);
                }
            }
        }

        var sonucListe = servisUrunleri
            .Select(s =>
            {
                var kart = modelKodlari.TryGetValue(s.UrunId, out var kod)
                    ? kartlar.GetValueOrDefault(kod) : null;
                return new
                {
                    urunId = s.UrunId,
                    urunAnaVaryantId = s.UrunAnaVaryantId,
                    score = s.Score,
                    match = s.Match,
                    imageUrl = kart?.ImageUrl,
                    productName = kart is null ? null
                        : kart.Name.GetValueOrDefault("tr") ?? kart.Name.Values.FirstOrDefault(),
                    modelCode = kart?.ModelCode,
                    price = kart?.Price,
                    productUrl = kart?.Url
                };
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
