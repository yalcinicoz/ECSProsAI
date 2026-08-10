namespace ECSPros.Shared.Contracts;

/// <summary>
/// Ürün listesi sıralama kataloğu (2026-08-10): site dropdown'ı + panel görünürlük
/// ayarı + query handler'lar aynı kod listesini kullanır. "default" her zaman vardır
/// (kapatılamaz — sıralama seçilmediğinde düşülen seçenek). Görünürlük kanal
/// Settings."productList"."sortOptions" sözlüğünden okunur (eksik anahtar = AÇIK).
/// </summary>
public static class ProductSortCatalog
{
    /// <summary>(Kod, site etiketi) — sırası dropdown sırasıdır.</summary>
    public static readonly IReadOnlyList<(string Kod, string Ad)> Tumu =
    [
        ("default", "Önerilen Sıralama"),
        ("price_asc", "En Düşük Fiyat"),
        ("price_desc", "En Yüksek Fiyat"),
        ("newest", "En Yeniler"),
        ("rating_desc", "En Yüksek Puanlı Ürünler"),
        ("reviews_desc", "En Fazla Yorum Alan Ürünler"),
        ("favorites_desc", "Favoriye En Çok Eklenen Ürünler"),
        ("cart_desc", "Sepete En Çok Atılan Ürünler"),
        ("views_desc", "En Çok Bakılan Ürünler"),
        ("sales_desc", "En Çok Satılan Ürünler"),
    ];

    /// <summary>Metrik (sayaç) tabanlı sıralama mı — bu kodlar IProductMetricsProvider ister.</summary>
    public static bool MetrikMi(string? kod) => kod is "rating_desc" or "reviews_desc"
        or "favorites_desc" or "cart_desc" or "views_desc" or "sales_desc";
}

/// <summary>Ürün başına sıralama metrikleri — dict'te olmayan ürün için Sifir kullanılır.</summary>
public sealed record ProductMetrics(
    double Rating,      // onaylı yorum ortalaması
    int ReviewCount,    // onaylı yorum sayısı
    int FavoriteCount,  // farklı üye favorisi
    int CartCount,      // son 30 günde farklı sepet
    int ViewCount,      // farklı üye görüntülemesi (viewed_products üye başına tek satır)
    int SalesCount)     // iptal olmayan siparişlerdeki toplam adet
{
    public static readonly ProductMetrics Sifir = new(0, 0, 0, 0, 0, 0);
}

/// <summary>
/// Kanalın TÜM ürünleri için sıralama metriklerini döner (yalnız en az bir metriği
/// sıfırdan farklı ürünler sözlükte yer alır). Implementasyon API host'undadır —
/// kaynaklar dört ayrı modül şemasında (storefront/crm/catalog/order) olduğundan tek
/// SQL bağlantısıyla şema-ötesi GROUP BY çalıştırılır ve sonuç süreç içinde cache'lenir;
/// sıralama anlık değil "güncel'e yakın" olmalıdır (TTL ~10 dk).
/// </summary>
public interface IProductMetricsProvider
{
    Task<IReadOnlyDictionary<Guid, ProductMetrics>> GetAsync(
        Guid firmPlatformId, CancellationToken ct = default);
}
