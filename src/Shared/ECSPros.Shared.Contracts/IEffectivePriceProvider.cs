namespace ECSPros.Shared.Contracts;

/// <summary>
/// B-005/B-006 (kabul testi 2026-07-22): ürün başına EFEKTİF minimum satış fiyatı —
/// kanal fiyat override'ı varsa o, yoksa varyant BasePrice (kartta gösterilen fiyatla
/// aynı öncelik). Genel listede fiyat sıralaması gösterilen fiyattan yapılmalı;
/// BasePrice ile sıralayıp kanal fiyatı göstermek yanlış sıra üretiyordu.
/// IInStockProductProvider kardeşi: tek raw SQL + platform bazlı kısa cache.
/// </summary>
public interface IEffectivePriceProvider
{
    Task<Dictionary<Guid, decimal>> GetMinEffectivePricesAsync(Guid firmPlatformId, CancellationToken ct = default);
}
