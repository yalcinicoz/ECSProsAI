namespace ECSPros.Shared.Contracts;

public interface IProductService
{
    Task<ProductInfo?> GetVariantAsync(Guid variantId, CancellationToken ct = default);
    Task<bool> VariantExistsAsync(Guid variantId, CancellationToken ct = default);

    /// <summary>
    /// Varyantların müşteriye dönük gösterim bilgisi (ad/görsel/seçenek özeti) — sepet,
    /// mini sepet ve sipariş satırları gibi Catalog dışı modüllerin satır zenginleştirmesi
    /// için (B5). Bulunamayan varyantlar sözlükte yer almaz.
    /// </summary>
    Task<Dictionary<Guid, VariantDisplayInfo>> GetVariantDisplayAsync(
        IReadOnlyCollection<Guid> variantIds, CancellationToken ct = default);
}

public record ProductInfo(
    Guid VariantId,
    string Sku,
    string ProductName,
    decimal BasePrice,
    bool IsActive,
    Guid ProductId = default);   // M2/M3: kanal seçimi/durdurma geçidi için (checkout)

public record VariantDisplayInfo(
    Guid VariantId,
    string ProductCode,
    Dictionary<string, string> ProductNameI18n,
    string? ImageUrl,
    string? OptionsText);   // ör. "Beden: M, Renk: Beyaz"
