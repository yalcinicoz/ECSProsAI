namespace ECSPros.Shared.Contracts.Channels;

/// <summary>
/// K17 kanal stok formülü (docs/satis-kanali-ortak-kurgu.md):
///   netStock       = satışa açık kısımlardaki (WarehouseSection.IsSellableOnline, aktif depo) stok − rezerv
///   stockQuantity  = max(0, netStock − minStock + 1)          (minStock kanal yeteneği; site=1)
/// Tek hesaplayıcı: kapsam kriteri (F1), Partner stok yanıtı (F4), pazaryeri stok gönderimi aynı çağrıyı kullanır.
/// </summary>
public interface IChannelStockCalculator
{
    /// <summary>Varyant → net stok (yalnız net &gt; 0 olanlar). Kısa süreli cache'li.</summary>
    Task<Dictionary<Guid, int>> GetVariantNetStocksAsync(CancellationToken ct = default);

    /// <summary>En az bir varyantında stockQuantity ≥ 1 (⇔ net ≥ minStock) olan ürün Id'leri.</summary>
    Task<HashSet<Guid>> GetProductIdsWithChannelStockAsync(int minStock, CancellationToken ct = default);

    static int ChannelQuantity(int netStock, int minStock) => Math.Max(0, netStock - Math.Max(1, minStock) + 1);
}
