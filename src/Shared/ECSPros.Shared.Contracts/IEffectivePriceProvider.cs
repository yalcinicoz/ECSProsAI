namespace ECSPros.Shared.Contracts;

/// <summary>
/// B-005/B-006 (kabul testi 2026-07-22): ürün başına KART fiyatı — kartta gösterilen fiyatla
/// AYNI kural (<see cref="KartFiyatGorunumu.KartTabanFiyati"/>: varyant başına kanal fiyatı ?? taban,
/// bunların en yükseği; 2026-09-10 kullanıcı kararıyla min → maks). Genel listede fiyat filtresi ve
/// sıralaması gösterilen fiyattan yapılmalı; BasePrice ile sıralayıp kanal fiyatı göstermek yanlış
/// sıra üretiyordu. Favori/sepet fiyat-düşüşü taraması da bu fiyatı izler (kart neyi gösteriyorsa o).
/// IInStockProductProvider kardeşi: tek raw SQL + platform bazlı kısa cache.
/// </summary>
public interface IEffectivePriceProvider
{
    Task<Dictionary<Guid, decimal>> GetKartFiyatlariAsync(Guid firmPlatformId, CancellationToken ct = default);
}
