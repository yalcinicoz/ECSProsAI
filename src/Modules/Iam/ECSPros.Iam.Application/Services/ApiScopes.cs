namespace ECSPros.Iam.Application.Services;

/// <summary>
/// Partner API scope kataloğu — panel `Permissions` kataloğundan BAĞIMSIZ (yeni panel yetkisi
/// dışa açılan API'yi genişletmesin). Scope "ne yapabilir"; owner "hangi veri" demektir.
/// Tip kataloğu (ApiClientType.BaseScopes) bu kodlardan oluşur; RequireScope (F2) bu kodlara bakar.
/// </summary>
public static class ApiScopes
{
    public const string CatalogRead = "catalog.read";        // ürün/kategori okuma
    public const string CatalogWrite = "catalog.write";      // ürün İÇERİĞİ (fiyat hariç)
    public const string PricingWrite = "pricing.write";      // fiyat + satış kuralları
    public const string StockRead = "stock.read";
    public const string StockWrite = "stock.write";
    public const string OrderRead = "order.read";
    public const string OrderWrite = "order.write";
    public const string FulfillmentWrite = "fulfillment.write"; // "kargoladım" + takip no
    public const string InvoiceRead = "invoice.read";
    public const string AccountRead = "account.read";        // cari ekstre / bakiye

    public static readonly string[] All =
    {
        CatalogRead, CatalogWrite, PricingWrite, StockRead, StockWrite,
        OrderRead, OrderWrite, FulfillmentWrite, InvoiceRead, AccountRead
    };

    /// <summary>Gönderim bayrağı (FulfillmentMode) supplier ise tabana eklenen scope'lar (Yol B).</summary>
    public static readonly string[] SupplierFulfillment = { OrderRead, FulfillmentWrite };
}
