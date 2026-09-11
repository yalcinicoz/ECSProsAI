namespace ECSPros.Catalog.Domain.Entities;

/// <summary>
/// Ürün liste filtreleri için türetilmiş istatistik (2026-09-11, admin ürün listesi "kapsamlı filtre"):
/// SALT OKUNUR okuma modeli — <c>catalog.mv_product_stats</c> MATERIALIZED VIEW'ına eşlenir (migration SQL'i oluşturur,
/// EF şemadan hariç tutar). <see cref="ECSPros.Api.Services.ProductStatsRefreshWorker"/> 5 dk'da bir CONCURRENTLY yeniler;
/// panel filtreleri bu tazelikle çalışır (görsel/stok değişimi en geç 5 dk sonra filtreye yansır).
/// Görsel durumu RENK bazlıdır: grubun birincil ekseni (IsPrimaryAxis, bugün hep "renk") değerine göre gruplanan
/// varyantlardan en az birinde product_images kaydı olan renk "görselli" sayılır. none = hiç görsel yok,
/// full = tüm renklerde var, partial = bazı renklerde var (eksensiz üründe: var/yok).
/// </summary>
public class ProductStats
{
    public Guid ProductId { get; set; }
    public string ImageState { get; set; } = "none";   // none | partial | full
    public int ColorCount { get; set; }
    public int ColorsWithImage { get; set; }
    public int ImageCount { get; set; }
    public DateTime? LastImageAt { get; set; }
    public int StockQuantity { get; set; }              // fiziksel toplam (inv_stocks.Quantity)
    public int StockAvailable { get; set; }             // kullanılabilir toplam (Quantity − Reserved)
    public DateTime RefreshedAt { get; set; }
}
