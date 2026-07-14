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
}
