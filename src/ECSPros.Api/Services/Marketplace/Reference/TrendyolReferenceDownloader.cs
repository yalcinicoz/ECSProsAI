using System.Text.Json;

namespace ECSPros.Api.Services.Marketplace.Reference;

/// <summary>
/// Trendyol referans indirici. Kategori ağacı ve kategori-özellik uçları kimliksiz
/// erişilebilir (2026-07-26 doğrulandı); satıcı kimliği gerektirmez. Base URL config
/// ile değiştirilebilir (Trendyol:ReferenceBaseUrl) — gateway adresi değişirse kod
/// değişikliği gerekmesin.
/// </summary>
public sealed class TrendyolReferenceDownloader(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration) : IMarketplaceReferenceDownloader
{
    public string ServiceCode => "trendyol";

    public TimeSpan AttributeRequestDelay { get; } = TimeSpan.FromMilliseconds(
        configuration.GetValue("Trendyol:ReferenceRequestDelayMs", 150));

    private string BaseUrl => configuration.GetValue<string>("Trendyol:ReferenceBaseUrl")
        ?? "https://apigw.trendyol.com/integration/product";

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient("TrendyolReference");
        client.Timeout = TimeSpan.FromSeconds(60);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ECSPros/1.0");
        return client;
    }

    public async Task<List<RefCategoryDto>> DownloadCategoriesAsync(CancellationToken ct = default)
    {
        using var client = CreateClient();
        using var response = await client.GetAsync($"{BaseUrl}/product-categories", ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var result = new List<RefCategoryDto>();
        if (doc.RootElement.TryGetProperty("categories", out var roots))
            foreach (var node in roots.EnumerateArray())
                Flatten(node, parentId: null, result);
        return result;
    }

    private static void Flatten(JsonElement node, string? parentId, List<RefCategoryDto> result)
    {
        var id = node.GetProperty("id").GetInt64().ToString();
        var name = node.GetProperty("name").GetString() ?? "";
        // Raw'a alt ağaç girmez — düğümün kendi alanları yeter, satır boyutu patlamaz.
        var raw = JsonSerializer.Serialize(new { id, name, parentId });
        result.Add(new RefCategoryDto(id, parentId, name, raw));

        if (node.TryGetProperty("subCategories", out var subs))
            foreach (var sub in subs.EnumerateArray())
                Flatten(sub, id, result);
    }

    public async Task<List<RefAttributeDto>> DownloadCategoryAttributesAsync(
        string categoryExternalId, CancellationToken ct = default)
    {
        using var client = CreateClient();
        using var response = await client.GetAsync(
            $"{BaseUrl}/product-categories/{Uri.EscapeDataString(categoryExternalId)}/attributes", ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var result = new List<RefAttributeDto>();
        if (!doc.RootElement.TryGetProperty("categoryAttributes", out var attrs))
            return result;

        foreach (var ca in attrs.EnumerateArray())
        {
            var attr = ca.GetProperty("attribute");
            var id = attr.GetProperty("id").GetInt64().ToString();
            var name = attr.GetProperty("name").GetString() ?? "";
            var required = ca.TryGetProperty("required", out var r) && r.GetBoolean();
            var allowCustom = ca.TryGetProperty("allowCustom", out var acEl) && acEl.GetBoolean();
            var varianter = ca.TryGetProperty("varianter", out var v) && v.GetBoolean();

            var values = new List<RefAttributeValueDto>();
            if (ca.TryGetProperty("attributeValues", out var vals))
                foreach (var av in vals.EnumerateArray())
                    values.Add(new RefAttributeValueDto(
                        av.GetProperty("id").GetInt64().ToString(),
                        Code: null,
                        av.GetProperty("name").GetString() ?? ""));

            var raw = JsonSerializer.Serialize(new { id, name, required, allowCustom, varianter });
            // Trendyol değer kimliği bekler (attributeValueId); serbest giriş allowCustom ile ayrıca işaretlidir.
            result.Add(new RefAttributeDto(
                id, Code: null, name, required, allowCustom, varianter,
                IsMultiValue: false, ValueMode: "id", raw, values));
        }
        return result;
    }
}
