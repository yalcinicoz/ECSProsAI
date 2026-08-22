namespace ECSPros.Shared.Contracts.Tracking;

/// <summary>
/// Merkezi commerce event sözleşmesi (İE-2 Faz B, 2026-08-22 — plan
/// docs/reklam-analytics-entegrasyon-is-akisi.md §4 + Faz B-1). İç olaylar (sipariş/sepet/üye)
/// tek biçime indirgenir; dış platformlara (GA4/Meta/TikTok…) neyin gideceği tamamen
/// adapter + kanal entegrasyonu + consent kararıdır. Çekirdek modüller yalnız bu tipi üretir.
/// </summary>
public sealed record CommerceEvent(
    string Name,                                   // CommerceEventNames sabitleri
    DateTime OccurredAt,
    Guid FirmPlatformId,
    string DedupId,                                // purchase: OrderId; diğer: tarayıcı/sunucu GUID
    string Source,                                 // web | mobile | server
    Guid? MemberId,
    string Currency,                               // TRY
    decimal? Value,                                // KDV dahil toplam
    string? TransactionId,                         // OrderNumber (okunur), purchase/refund
    IReadOnlyList<CommerceItem> Items,
    ClientContext Client,
    ConsentState Consent,
    IReadOnlyDictionary<string, string> Extra);    // coupon, shipping, tax, search_term, list_id …

public sealed record CommerceItem(
    string ItemId,                                 // varyant SKU/barkod (satılabilir birim)
    string ItemGroupId,                            // ürün kodu
    string Name,
    string? Brand,
    string? Category,
    string? Variant,                               // "Renk: Siyah, Beden: M"
    decimal Price,                                 // KDV dahil birim fiyat
    int Quantity,
    decimal Discount);                             // kaleme dağıtılmış indirim (toplam)

/// <summary>Tarayıcı/cihaz eşleştirme bağlamı — server-side gönderimlerde eşleşme kalitesi için
/// (Meta fbp/fbc + hash'li e-posta/telefon, GA4 client_id, TikTok ttclid, Google gclid).
/// PII yalnız SHA256 hex (lowercase) — ham e-posta/telefon ASLA taşınmaz.</summary>
public sealed record ClientContext(
    string? Ip,
    string? UserAgent,
    string? Fbp,
    string? Fbc,
    string? GaClientId,
    string? TtClickId,
    string? Gclid,
    string? PageUrl,
    string? Referrer,
    string? EmailSha256,
    string? PhoneSha256,
    string? ExternalIdSha256)
{
    public static readonly ClientContext Bos = new(null, null, null, null, null, null, null, null, null, null, null, null);
}

/// <summary>Kategori bazlı izin (KVKK/GDPR + Google Consent Mode v2). Varsayılan: hepsi false (deny).</summary>
public sealed record ConsentState(bool Analytics, bool Ads, bool Personalization)
{
    public static readonly ConsentState Deny = new(false, false, false);
    public static readonly ConsentState Grant = new(true, true, true);
}

public static class CommerceEventNames
{
    public const string ProductViewed = "product_viewed";
    public const string ProductListViewed = "product_list_viewed";
    public const string Search = "search";
    public const string AddedToCart = "added_to_cart";
    public const string RemovedFromCart = "removed_from_cart";
    public const string CartViewed = "cart_viewed";
    public const string CheckoutStarted = "checkout_started";
    public const string ShippingInfoAdded = "shipping_info_added";
    public const string PaymentInfoAdded = "payment_info_added";
    public const string OrderCompleted = "order_completed";
    public const string Refund = "refund";
    public const string SignUp = "sign_up";
    public const string Login = "login";
    public const string WishlistAdded = "wishlist_added";
    public const string NewsletterSubscribed = "newsletter_subscribed";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        ProductViewed, ProductListViewed, Search, AddedToCart, RemovedFromCart, CartViewed,
        CheckoutStarted, ShippingInfoAdded, PaymentInfoAdded, OrderCompleted, Refund,
        SignUp, Login, WishlistAdded, NewsletterSubscribed
    };

    public static bool IsValid(string? name) => name is not null && All.Contains(name);
}

/// <summary>Event'i kalıcı kuyruğa (outbox) yazar. HATA FIRLATMAZ — checkout/üyelik akışı
/// tracking yüzünden asla bozulmaz. Tracking kapalıysa (Tracking:Enabled=false) sessizce atlar.</summary>
public interface ICommerceEventPublisher
{
    Task PublishAsync(CommerceEvent commerceEvent, CancellationToken ct = default);
}
