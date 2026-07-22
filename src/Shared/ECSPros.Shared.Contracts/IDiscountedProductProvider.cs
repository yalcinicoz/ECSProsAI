namespace ECSPros.Shared.Contracts;

/// <summary>
/// H10: kanalda indirimli (CompareAtPrice &gt; Price) ürün Id kümesi — vitrin "indirimli"
/// kaynak bayrağı için. IInStockProductProvider kardeşi: cross-schema tek raw SQL + kısa cache.
/// </summary>
public interface IDiscountedProductProvider
{
    Task<HashSet<Guid>> GetDiscountedProductIdsAsync(Guid firmPlatformId, CancellationToken ct = default);
}
