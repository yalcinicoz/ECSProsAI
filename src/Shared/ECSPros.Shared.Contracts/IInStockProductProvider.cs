namespace ECSPros.Shared.Contracts;

/// <summary>
/// Online satılabilir stoğu olan (SUM serbest > 0, yalnız satışa-açık kısımlar/aktif depo)
/// ürün Id'leri — liste görünürlüğü stok filtresi için (2026-07-14). Kısa süreli cache'li
/// (stok görece stabil; tek raw-SQL ile inventory↔catalog join). "Stoğu biten ürün" = bu
/// kümede OLMAYAN ürün.
/// </summary>
public interface IInStockProductProvider
{
    Task<HashSet<Guid>> GetInStockProductIdsAsync(CancellationToken ct = default);

    /// <summary>
    /// Online satılabilir stoğu olan VARYANT Id'leri (SUM serbest > 0, yalnız satışa-açık
    /// kısımlar/aktif depo). Renk-modu listelemesinde kart = (ürün × renk); ürünün stoğu biten
    /// rengini (o rengin tüm varyantları bu kümede yoksa) elemek için kullanılır — ürün düzeyi
    /// küme yalnız TÜM renkleri stoksuz ürünü eler. Aynı raw-SQL, kısa süreli cache'li.
    /// </summary>
    Task<HashSet<Guid>> GetInStockVariantIdsAsync(CancellationToken ct = default);
}
