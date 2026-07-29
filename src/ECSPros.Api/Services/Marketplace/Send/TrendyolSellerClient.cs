using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ECSPros.Core.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.Marketplace.Send;

/// <summary>Sözleşmeden çözülen Trendyol satıcı yapılandırması. brandId ve cargoCompanyId
/// Trendyol ürün gönderiminde zorunludur — sözleşmenin Settings alanına girilir.</summary>
public sealed record TrendyolSellerConfig(
    string SupplierId, string ApiKey, string ApiSecret, long? BrandId, long? CargoCompanyId);

public sealed record TrendyolBatchItemResult(string Barcode, string Status, string? FailureReason);

public sealed record TrendyolBatchStatus(
    string BatchRequestId, string? OverallStatus, List<TrendyolBatchItemResult> Items);

/// <summary>
/// Trendyol Satıcı API istemcisi (F4). Base URL config ile değiştirilebilir
/// (Trendyol:SellerBaseUrl) — hem gateway değişikliğine hem izole mock testine imkân verir.
/// Auth: Basic base64(apiKey:apiSecret) + User-Agent "{supplierId} - SelfIntegration".
/// </summary>
public sealed class TrendyolSellerClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ICoreDbContext coreDb)
{
    private string BaseUrl => (configuration.GetValue<string>("Trendyol:SellerBaseUrl")
        ?? "https://apigw.trendyol.com/integration/product").TrimEnd('/');

    /// <summary>Sözleşmeden (FirmPlatformIntegration) kimlik + ayarları çözer.
    /// Credentials EF converter'ıyla şifresi çözülmüş gelir.</summary>
    public async Task<(TrendyolSellerConfig? Config, string? Error)> ResolveConfigAsync(
        Guid firmIntegrationId, CancellationToken ct)
    {
        var fi = await coreDb.FirmPlatformIntegrations
            .Where(x => x.Id == firmIntegrationId && x.IsActive)
            .Select(x => new { x.Credentials, x.Settings })
            .FirstOrDefaultAsync(ct);
        if (fi is null) return (null, "Aktif pazaryeri sözleşmesi bulunamadı.");

        string? Get(Dictionary<string, object> d, string key) =>
            d.TryGetValue(key, out var v) ? v switch
            {
                string s => s,
                JsonElement je => je.ValueKind == JsonValueKind.String ? je.GetString() : je.ToString(),
                _ => v?.ToString()
            } : null;

        var apiKey = Get(fi.Credentials, "apiKey");
        var apiSecret = Get(fi.Credentials, "apiSecret");
        var supplierId = Get(fi.Settings, "supplierId");
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret) || string.IsNullOrWhiteSpace(supplierId))
            return (null, "Trendyol API bilgileri eksik — sözleşmede supplierId, apiKey ve apiSecret dolu olmalı.");

        long? brandId = long.TryParse(Get(fi.Settings, "brandId"), out var b) ? b : null;
        long? cargoId = long.TryParse(Get(fi.Settings, "cargoCompanyId"), out var c) ? c : null;
        return (new TrendyolSellerConfig(supplierId.Trim(), apiKey, apiSecret, brandId, cargoId), null);
    }

    private HttpClient Create(TrendyolSellerConfig cfg)
    {
        var client = httpClientFactory.CreateClient("TrendyolSeller");
        client.Timeout = TimeSpan.FromSeconds(90);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{cfg.ApiKey}:{cfg.ApiSecret}")));
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", $"{cfg.SupplierId} - SelfIntegration");
        return client;
    }

    /// <summary>Ürün oluşturma/güncelleme — batchRequestId döner (sonuç asenkron sorgulanır, K7).</summary>
    public async Task<string> CreateProductsAsync(
        TrendyolSellerConfig cfg, object payload, CancellationToken ct)
    {
        using var client = Create(cfg);
        using var response = await client.PostAsJsonAsync(
            $"{BaseUrl}/sellers/{cfg.SupplierId}/products", payload, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Trendyol gönderim isteği reddedildi (HTTP {(int)response.StatusCode}): {Truncate(body, 500)}");

        using var doc = JsonDocument.Parse(body);
        var id = doc.RootElement.TryGetProperty("batchRequestId", out var bid) ? bid.GetString() : null;
        return id ?? throw new InvalidOperationException($"Trendyol cevabında batchRequestId yok: {Truncate(body, 300)}");
    }

    /// <summary>Batch sonucu — kısmi cevap normaldir: yalnız değerlendirilmiş item'lar döner,
    /// kalanı sonraki sorguda gelir. Barkod eşleşmesi esas alınır.</summary>
    public async Task<TrendyolBatchStatus?> GetBatchStatusAsync(
        TrendyolSellerConfig cfg, string batchRequestId, CancellationToken ct)
    {
        using var client = Create(cfg);
        using var response = await client.GetAsync(
            $"{BaseUrl}/sellers/{cfg.SupplierId}/products/batch-requests/{Uri.EscapeDataString(batchRequestId)}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;

        var items = new List<TrendyolBatchItemResult>();
        if (root.TryGetProperty("items", out var arr))
            foreach (var item in arr.EnumerateArray())
            {
                var barcode = FindBarcode(item);
                if (barcode is null) continue;
                var status = item.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "";
                string? failure = null;
                if (item.TryGetProperty("failureReasons", out var fr) && fr.ValueKind == JsonValueKind.Array)
                    failure = string.Join(" | ", fr.EnumerateArray()
                        .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.ToString())
                        .Where(s => !string.IsNullOrWhiteSpace(s)));
                items.Add(new TrendyolBatchItemResult(barcode, status, failure));
            }

        return new TrendyolBatchStatus(
            batchRequestId,
            root.TryGetProperty("status", out var os) ? os.GetString() : null,
            items);
    }

    /// <summary>Fiyat-stok hızlı kanalı (F5) — aynı batch takip altyapısından geçer.</summary>
    public async Task<string> UpdatePriceInventoryAsync(
        TrendyolSellerConfig cfg, object payload, CancellationToken ct)
    {
        using var client = Create(cfg);
        using var response = await client.PostAsJsonAsync(
            $"{BaseUrl}/sellers/{cfg.SupplierId}/products/price-and-inventory", payload, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Trendyol fiyat-stok isteği reddedildi (HTTP {(int)response.StatusCode}): {Truncate(body, 500)}");
        using var doc = JsonDocument.Parse(body);
        var id = doc.RootElement.TryGetProperty("batchRequestId", out var bid) ? bid.GetString() : null;
        return id ?? throw new InvalidOperationException($"Trendyol cevabında batchRequestId yok: {Truncate(body, 300)}");
    }

    public sealed record TrendyolListingItem(
        string Barcode, int? Quantity, decimal? SalePrice, long? CategoryId, bool OnSale);

    /// <summary>Satıcının yüklü ürünlerini sayfalı çeker (F5 mutabakat).</summary>
    public async Task<(List<TrendyolListingItem> Items, int TotalPages)> GetProductsPageAsync(
        TrendyolSellerConfig cfg, int page, int size, CancellationToken ct)
    {
        using var client = Create(cfg);
        using var response = await client.GetAsync(
            $"{BaseUrl}/sellers/{cfg.SupplierId}/products?page={page}&size={size}", ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;

        var items = new List<TrendyolListingItem>();
        if (root.TryGetProperty("content", out var content))
            foreach (var p in content.EnumerateArray())
            {
                var barcode = p.TryGetProperty("barcode", out var b) ? b.GetString() : null;
                if (string.IsNullOrWhiteSpace(barcode)) continue;
                items.Add(new TrendyolListingItem(
                    barcode,
                    p.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.Number ? q.GetInt32() : null,
                    p.TryGetProperty("salePrice", out var sp) && sp.ValueKind == JsonValueKind.Number ? sp.GetDecimal() : null,
                    p.TryGetProperty("pimCategoryId", out var pc) && pc.ValueKind == JsonValueKind.Number ? pc.GetInt64()
                        : p.TryGetProperty("categoryId", out var cc) && cc.ValueKind == JsonValueKind.Number ? cc.GetInt64() : null,
                    p.TryGetProperty("onSale", out var os) && os.ValueKind is JsonValueKind.True or JsonValueKind.False && os.GetBoolean()));
            }
        var totalPages = root.TryGetProperty("totalPages", out var tp) && tp.ValueKind == JsonValueKind.Number
            ? tp.GetInt32() : 1;
        return (items, totalPages);
    }

    // requestItem doğrudan ürün alanlarını da, sarmalanmış "product" nesnesini de taşıyabiliyor.
    private static string? FindBarcode(JsonElement item)
    {
        if (item.TryGetProperty("requestItem", out var ri))
        {
            if (ri.TryGetProperty("barcode", out var b1) && b1.ValueKind == JsonValueKind.String) return b1.GetString();
            if (ri.TryGetProperty("product", out var p) && p.TryGetProperty("barcode", out var b2)
                && b2.ValueKind == JsonValueKind.String) return b2.GetString();
        }
        if (item.TryGetProperty("barcode", out var b3) && b3.ValueKind == JsonValueKind.String) return b3.GetString();
        return null;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
