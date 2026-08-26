using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ECSPros.Integration.Application.Adapters;
using Microsoft.Extensions.Logging;

namespace ECSPros.Integration.Infrastructure.Adapters.Marketplace;

/// <summary>Sözleşmeden çözülen Amazon SP-API yapılandırması.</summary>
public sealed record AmazonConfig(
    string SellerId,
    string AccessKey,
    string SecretKey,
    string RefreshToken,
    string MarketplaceId,
    string BaseUrl,
    string LwaTokenUrl,
    string Currency,
    string IssueLocale);

/// <summary>
/// Amazon Selling Partner API (SP-API) istemcisi. Kimliksiz başlatılabilir; gerçek HTTP
/// çağrıları sözleşmeden çözülen kimliklerle yapılır. Uçlar:
///   token      → POST {LwaTokenUrl} (grant_type=refresh_token)
///   ürün       → PUT  {BaseUrl}/listings/2021-08-01/items/{sellerId}/{sku}
///   stok/fiyat → PATCH aynı uç (purchasable_offer + fulfillment_availability)
///   sipariş    → GET  {BaseUrl}/orders/v0/orders (+ kalemleri için /orderItems)
/// Region, base URL ve LWA URL sözleşmenin Settings alanından ezilebilir
/// (region=na|eu|fe, baseUrl, lwaTokenUrl) — izole test/mock'a da imkân verir.
/// </summary>
public sealed class AmazonSpApiClient(
    IHttpClientFactory httpClientFactory,
    ILogger<AmazonSpApiClient> logger)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    /// <summary>FirmPlatformIntegration'dan Amazon kimlik + ayarlarını çözer.
    /// Credentials EF converter'ıyla şifresi çözülmüş gelir.</summary>
    public async Task<(AmazonConfig? Config, string? Error)> ResolveConfigAsync(
        IMarketplaceCredentialResolver resolver, Guid firmIntegrationId, CancellationToken ct)
    {
        var cred = await resolver.ResolveAsync(firmIntegrationId, ct);
        if (cred is null) return (null, "Aktif Amazon sözleşmesi bulunamadı.");

        string? Get(Dictionary<string, object> d, string key) =>
            d.TryGetValue(key, out var v) ? v switch
            {
                string s => s,
                JsonElement je => je.ValueKind == JsonValueKind.String ? je.GetString() : je.ToString(),
                _ => v?.ToString()
            } : null;

        var sellerId = Get(cred.Settings, "sellerId");
        var marketplaceId = Get(cred.Settings, "marketplaceId");
        var accessKey = Get(cred.Credentials, "accessKey");
        var secretKey = Get(cred.Credentials, "secretKey");
        var refreshToken = Get(cred.Credentials, "refreshToken");

        if (string.IsNullOrWhiteSpace(sellerId) || string.IsNullOrWhiteSpace(accessKey)
            || string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(refreshToken)
            || string.IsNullOrWhiteSpace(marketplaceId))
            return (null, "Amazon bilgileri eksik — sözleşmede sellerId, marketplaceId, accessKey, secretKey ve refreshToken dolu olmalı.");

        var region = (Get(cred.Settings, "region") ?? "eu").Trim().ToLowerInvariant();
        var (defaultBase, defaultLwa) = region switch
        {
            "na" => ("https://sellingpartnerapi-na.amazon.com", "https://api.amazon.com/auth/o2/token"),
            "fe" => ("https://sellingpartnerapi-fe.amazon.com", "https://api.amazon.co.jp/auth/o2/token"),
            _ => ("https://sellingpartnerapi-eu.amazon.com", "https://api.amazon.co.uk/auth/o2/token"),
        };

        var baseUrl = Get(cred.Settings, "baseUrl");
        var lwaUrl = Get(cred.Settings, "lwaTokenUrl");
        var currency = Get(cred.Settings, "currency") ?? "TRY";
        var locale = Get(cred.Settings, "issueLocale") ?? "en_US";

        return (new AmazonConfig(
            sellerId.Trim(),
            accessKey,
            secretKey,
            refreshToken,
            marketplaceId.Trim(),
            (baseUrl ?? defaultBase).TrimEnd('/'),
            (lwaUrl ?? defaultLwa).TrimEnd('/'),
            currency.Trim(),
            locale.Trim()), null);
    }

    /// <summary>LWA token değişimi (refresh_token → access_token).</summary>
    public async Task<string> GetAccessTokenAsync(AmazonConfig cfg, CancellationToken ct)
    {
        using var client = httpClientFactory.CreateClient("AmazonSpApi");
        client.Timeout = TimeSpan.FromSeconds(30);

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = cfg.RefreshToken,
            ["client_id"] = cfg.AccessKey,
            ["client_secret"] = cfg.SecretKey,
        });

        using var response = await client.PostAsync(cfg.LwaTokenUrl, content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Amazon LWA token alınamadı (HTTP {(int)response.StatusCode}): {Truncate(body, 500)}");

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("access_token", out var tok) || string.IsNullOrWhiteSpace(tok.GetString()))
            throw new InvalidOperationException($"Amazon LWA cevabında access_token yok: {Truncate(body, 300)}");

        return tok.GetString()!;
    }

    /// <summary>Ürün listeleme — Listings Items PUT (SKU bazında oluştur/güncelle).
    /// productType ve zorunlu alanlar ürün tipine göre değişir; payload.Attributes'daki
    /// "productType" ile ezilebilir, yoksa "PRODUCT" varsayılır.</summary>
    public async Task<string> PutListingAsync(
        AmazonConfig cfg, MarketplaceProductPayload payload, CancellationToken ct)
    {
        var sku = Sku(payload);
        var token = await GetAccessTokenAsync(cfg, ct);

        var productType = Attribute(payload, "productType") ?? "PRODUCT";
        var requestBody = new Dictionary<string, object?>
        {
            ["productType"] = productType,
            ["requirements"] = "LISTING",
            ["attributes"] = BuildListingAttributes(payload, cfg.Currency),
        };

        using var client = CreateClient(cfg, token);
        using var response = await client.PutAsync(ListingUrl(cfg, sku), JsonContent(requestBody), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Amazon listing gönderilemedi (HTTP {(int)response.StatusCode}): {Truncate(body, 600)}");

        logger.LogInformation("Amazon listing gönderildi: SKU={Sku}, SellerId={SellerId}, Marketplace={MarketplaceId}",
            sku, cfg.SellerId, cfg.MarketplaceId);
        return sku;
    }

    /// <summary>Fiyat + stok güncelleme — Listings Items PATCH (price ve/veya quantity).</summary>
    public async Task PatchListingAsync(
        AmazonConfig cfg, string sku, decimal? price, int? quantity, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(cfg, ct);

        var patches = new List<Dictionary<string, object?>>();
        if (price is { } p)
            patches.Add(new Dictionary<string, object?>
            {
                ["op"] = "replace",
                ["path"] = "/attributes/purchasable_offer",
                ["value"] = new[]
                {
                    new
                    {
                        currency = cfg.Currency,
                        our_price = new[] { new { schedule = new[] { new { value_with_tax = p } } } },
                    },
                },
            });
        if (quantity is { } q)
            patches.Add(new Dictionary<string, object?>
            {
                ["op"] = "replace",
                ["path"] = "/attributes/fulfillment_availability",
                ["value"] = new[]
                {
                    new { fulfillment_channel_code = "DEFAULT", quantity = Math.Max(0, q) },
                },
            });

        if (patches.Count == 0) return;

        var requestBody = new Dictionary<string, object?>
        {
            ["productType"] = "PRODUCT",
            ["patches"] = patches,
        };

        using var client = CreateClient(cfg, token);
        using var response = await client.PatchAsync(ListingUrl(cfg, sku), JsonContent(requestBody), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Amazon fiyat/stok güncellenemedi (HTTP {(int)response.StatusCode}): {Truncate(body, 600)}");

        logger.LogInformation("Amazon fiyat/stok güncellendi: SKU={Sku}, Price={Price}, Qty={Qty}", sku, price, quantity);
    }

    /// <summary>Sipariş çekimi — Orders API GET /orders/v0/orders (CreatedAfter ile artımlı,
    /// NextToken ile sayfalı) + kalemleri için /orderItems.</summary>
    public async Task<List<MarketplaceOrderDto>> FetchOrdersAsync(
        AmazonConfig cfg, DateTime? since, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(cfg, ct);

        // Amazon en fazla 7 günlük CreatedAfter penceresi kabul eder.
        var createdAfter = (since ?? DateTime.UtcNow.AddDays(-1)).ToUniversalTime();
        if (createdAfter < DateTime.UtcNow.AddDays(-7))
            createdAfter = DateTime.UtcNow.AddDays(-7);

        var orders = new List<MarketplaceOrderDto>();
        string? nextToken = null;
        const int maxPages = 200;

        for (var page = 0; page < maxPages; page++)
        {
            var url = $"{cfg.BaseUrl}/orders/v0/orders" +
                      $"?MarketplaceIds={Uri.EscapeDataString(cfg.MarketplaceId)}" +
                      $"&CreatedAfter={Uri.EscapeDataString(createdAfter.ToString("yyyy-MM-ddTHH:mm:ssZ"))}" +
                      "&OrderStatuses=Unshipped&OrderStatuses=PartiallyShipped";
            if (nextToken is not null)
                url += $"&NextToken={Uri.EscapeDataString(nextToken)}";

            using var client = CreateClient(cfg, token);
            using var response = await client.GetAsync(url, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Amazon sipariş çekilemedi (HTTP {(int)response.StatusCode}): {Truncate(body, 600)}");

            var (pageOrders, tokenOut) = ParseOrdersPage(body);
            orders.AddRange(pageOrders);
            nextToken = tokenOut;
            if (nextToken is null) break;
            if (page == maxPages - 1)
                logger.LogWarning("Amazon sipariş sayfalama üst sınıra ulaştı (MarketplaceId={MarketplaceId}); kalan sayfalar bir sonraki çalıştırmada çekilecek.", cfg.MarketplaceId);
        }

        // Kalemler ayrı uçtan gelir — siparişlerin satırlarını doldur.
        foreach (var order in orders)
        {
            order.Lines.AddRange(await FetchOrderItemsAsync(cfg, token, order.ExternalOrderId, ct));
        }

        return orders;
    }

    private async Task<List<MarketplaceOrderLineDto>> FetchOrderItemsAsync(
        AmazonConfig cfg, string token, string orderId, CancellationToken ct)
    {
        using var client = CreateClient(cfg, token);
        using var response = await client.GetAsync(
            $"{cfg.BaseUrl}/orders/v0/orders/{Uri.EscapeDataString(orderId)}/orderItems", ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Amazon sipariş kalemi çekilemedi: OrderId={OrderId}, HTTP={(int)response.StatusCode}",
                orderId, (int)response.StatusCode);
            return [];
        }
        return ParseOrderItems(body);
    }

    // ── yardımcılar ──────────────────────────────────────────────────────

    private string ListingUrl(AmazonConfig cfg, string sku) =>
        $"{cfg.BaseUrl}/listings/2021-08-01/items/{Uri.EscapeDataString(cfg.SellerId)}/{Uri.EscapeDataString(sku)}" +
        $"?marketplaceIds={Uri.EscapeDataString(cfg.MarketplaceId)}&issueLocale={Uri.EscapeDataString(cfg.IssueLocale)}";

    private HttpClient CreateClient(AmazonConfig cfg, string token)
    {
        var client = httpClientFactory.CreateClient("AmazonSpApi");
        client.Timeout = TimeSpan.FromSeconds(90);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.TryAddWithoutValidation("x-amz-access-token", token);
        return client;
    }

    private static StringContent JsonContent(object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOpts);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static string Sku(MarketplaceProductPayload p) =>
        !string.IsNullOrWhiteSpace(p.Barcode) ? p.Barcode.Trim() : p.VariantId.ToString("N");

    private static string? Attribute(MarketplaceProductPayload p, string key) =>
        p.Attributes is not null && p.Attributes.TryGetValue(key, out var v) ? v : null;

    private static Dictionary<string, object?> BuildListingAttributes(
        MarketplaceProductPayload p, string currency)
    {
        var attrs = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(p.Title))
            attrs["item_name"] = new[] { new { value = p.Title, language_tag = "tr_TR" } };
        if (!string.IsNullOrWhiteSpace(p.Description))
        {
            attrs["item_description"] = new[] { new { value = p.Description, language_tag = "tr_TR" } };
            attrs["product_description"] = new[] { new { value = p.Description, language_tag = "tr_TR" } };
        }
        if (!string.IsNullOrWhiteSpace(p.Barcode))
            attrs["external_product_id"] = new[] { new { type = "EAN", value = p.Barcode } };
        if (Attribute(p, "brand") is { } brand && !string.IsNullOrWhiteSpace(brand))
            attrs["brand"] = new[] { new { value = brand } };
        if (Attribute(p, "manufacturer") is { } mfr && !string.IsNullOrWhiteSpace(mfr))
            attrs["manufacturer"] = new[] { new { value = mfr } };
        if (p.Price > 0)
            attrs["purchasable_offer"] = new[]
            {
                new
                {
                    currency,
                    our_price = new[] { new { schedule = new[] { new { value_with_tax = p.Price } } } },
                },
            };
        attrs["fulfillment_availability"] = new[]
        {
            new { fulfillment_channel_code = "DEFAULT", quantity = Math.Max(0, p.StockQuantity) },
        };

        return attrs;
    }

    private static (List<MarketplaceOrderDto> Orders, string? NextToken) ParseOrdersPage(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var payload = root.TryGetProperty("payload", out var pl) ? pl : root;

        string? nextToken = null;
        if (payload.TryGetProperty("NextToken", out var nt) && nt.ValueKind == JsonValueKind.String)
            nextToken = nt.GetString();

        var orders = new List<MarketplaceOrderDto>();
        if (!payload.TryGetProperty("Orders", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return (orders, nextToken);

        foreach (var o in arr.EnumerateArray())
        {
            var id = Prop(o, "AmazonOrderId");
            if (string.IsNullOrWhiteSpace(id)) continue;

            var shipping = o.TryGetProperty("ShippingAddress", out var sa) ? sa : default;
            var name = Prop(shipping, "Name") ?? "Amazon Alıcısı";
            var phone = Prop(shipping, "Phone") ?? "";
            var address = FormatAddress(shipping);
            var orderDate = DateTime.TryParse(Prop(o, "PurchaseDate"), out var d) ? d : DateTime.UtcNow;
            var total = ParseAmount(o.TryGetProperty("OrderTotal", out var ot) ? ot : default);
            var currency = o.TryGetProperty("OrderTotal", out var otc)
                ? ParseCurrency(otc) : "TRY";

            orders.Add(new MarketplaceOrderDto(
                id,
                name,
                phone,
                address,
                total,
                currency,
                orderDate,
                []));
        }

        return (orders, nextToken);
    }

    private static List<MarketplaceOrderLineDto> ParseOrderItems(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var payload = root.TryGetProperty("payload", out var pl) ? pl : root;

        var lines = new List<MarketplaceOrderLineDto>();
        if (!payload.TryGetProperty("OrderItems", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return lines;

        foreach (var it in arr.EnumerateArray())
        {
            var sku = Prop(it, "SellerSKU") ?? Prop(it, "ASIN");
            if (string.IsNullOrWhiteSpace(sku)) continue;
            lines.Add(new MarketplaceOrderLineDto(
                sku,
                "",
                ReadInt(it, "QuantityOrdered") ?? 1,
                it.TryGetProperty("ItemPrice", out var ip) ? ParseAmount(ip) : 0));
        }

        return lines;
    }

    private static string? Prop(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    private static int? ReadInt(JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) return i;
        if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var si)) return si;
        return null;
    }

    private static string FormatAddress(JsonElement sa)
    {
        if (sa.ValueKind != JsonValueKind.Object) return "";
        var parts = new[]
        {
            Prop(sa, "AddressLine1"), Prop(sa, "AddressLine2"), Prop(sa, "AddressLine3"),
            Prop(sa, "City"), Prop(sa, "County"), Prop(sa, "StateOrRegion"),
            Prop(sa, "PostalCode"), Prop(sa, "CountryCode"),
        };
        return string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static decimal ParseAmount(JsonElement e)
    {
        if (e.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return 0;
        if (e.TryGetProperty("Amount", out var a)) return ParseMoney(a);
        return ParseMoney(e);
    }

    private static decimal ParseMoney(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Number && e.TryGetDecimal(out var d)) return d;
        if (e.ValueKind == JsonValueKind.String
            && decimal.TryParse(e.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var ds))
            return ds;
        return 0;
    }

    private static string ParseCurrency(JsonElement e)
    {
        if (e.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return "TRY";
        return e.TryGetProperty("CurrencyCode", out var c) && c.ValueKind == JsonValueKind.String
            ? c.GetString() ?? "TRY" : "TRY";
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
