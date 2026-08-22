using System.Text;
using System.Text.Json;
using ECSPros.Shared.Contracts.Tracking;

namespace ECSPros.Api.Services.Tracking.Adapters;

/// <summary>Adapter'ların ortak yardımcıları (İE-4 Faz D): HTTP gönderim, test event işareti, ad eşleme.</summary>
public static class TrackingAdapterBase
{
    public const string HttpClientName = "tracking";

    /// <summary>Test event'i (panel "Test event gönder") — Extra["test"]=="true".</summary>
    public static bool TestMi(CommerceEvent e) => e.Extra.TryGetValue("test", out var t) && t == "true";

    public static long UnixSaniye(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();

    public static string Json(object o) => JsonSerializer.Serialize(o, OutboxCommerceEventPublisher.JsonAyar);

    public static async Task<TrackingSendResult> PostJsonAsync(HttpClient http, string url, object body, CancellationToken ct, Action<HttpRequestMessage>? basliklar = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(Json(body), Encoding.UTF8, "application/json")
        };
        basliklar?.Invoke(req);
        try
        {
            using var yanit = await http.SendAsync(req, ct);
            var govde = await yanit.Content.ReadAsStringAsync(ct);
            var ozet = govde.Length > 500 ? govde[..500] : govde;
            return yanit.IsSuccessStatusCode
                ? TrackingSendResult.Ok((int)yanit.StatusCode, ozet)
                : TrackingSendResult.Fail($"HTTP {(int)yanit.StatusCode}: {ozet}", (int)yanit.StatusCode);
        }
        catch (TaskCanceledException)
        {
            return TrackingSendResult.Fail("zaman aşımı (5 sn)");
        }
        catch (HttpRequestException ex)
        {
            return TrackingSendResult.Fail("bağlantı hatası: " + ex.Message);
        }
    }

    /// <summary>§4.3 eşleme — iç ad → Meta standart event adı (null = gönderilmez).</summary>
    public static string? MetaAdi(string name) => name switch
    {
        CommerceEventNames.ProductViewed => "ViewContent",
        CommerceEventNames.Search => "Search",
        CommerceEventNames.AddedToCart => "AddToCart",
        CommerceEventNames.CheckoutStarted => "InitiateCheckout",
        CommerceEventNames.PaymentInfoAdded => "AddPaymentInfo",
        CommerceEventNames.OrderCompleted => "Purchase",
        CommerceEventNames.SignUp => "CompleteRegistration",
        CommerceEventNames.WishlistAdded => "AddToWishlist",
        CommerceEventNames.NewsletterSubscribed => "Lead",
        _ => null
    };

    /// <summary>§4.3 eşleme — iç ad → TikTok event adı.</summary>
    public static string? TikTokAdi(string name) => name switch
    {
        CommerceEventNames.ProductViewed => "ViewContent",
        CommerceEventNames.Search => "Search",
        CommerceEventNames.AddedToCart => "AddToCart",
        CommerceEventNames.CheckoutStarted => "InitiateCheckout",
        CommerceEventNames.PaymentInfoAdded => "AddPaymentInfo",
        CommerceEventNames.OrderCompleted => "CompletePayment",
        CommerceEventNames.SignUp => "CompleteRegistration",
        CommerceEventNames.WishlistAdded => "AddToWishlist",
        CommerceEventNames.NewsletterSubscribed => "SubmitForm",
        _ => null
    };

    /// <summary>§4.3 eşleme — iç ad → GA4 event adı.</summary>
    public static string Ga4Adi(string name) => name switch
    {
        CommerceEventNames.ProductViewed => "view_item",
        CommerceEventNames.ProductListViewed => "view_item_list",
        CommerceEventNames.Search => "search",
        CommerceEventNames.AddedToCart => "add_to_cart",
        CommerceEventNames.RemovedFromCart => "remove_from_cart",
        CommerceEventNames.CartViewed => "view_cart",
        CommerceEventNames.CheckoutStarted => "begin_checkout",
        CommerceEventNames.ShippingInfoAdded => "add_shipping_info",
        CommerceEventNames.PaymentInfoAdded => "add_payment_info",
        CommerceEventNames.OrderCompleted => "purchase",
        CommerceEventNames.Refund => "refund",
        CommerceEventNames.SignUp => "sign_up",
        CommerceEventNames.Login => "login",
        CommerceEventNames.WishlistAdded => "add_to_wishlist",
        CommerceEventNames.NewsletterSubscribed => "generate_lead",
        _ => name
    };
}
