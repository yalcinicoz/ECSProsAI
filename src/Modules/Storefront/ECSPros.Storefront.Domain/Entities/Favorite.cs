using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// E5: üyenin favori ürünü — kalp butonları (liste/detay/sepet) buna bağlanır.
/// Anahtar ProductCode'dur (kartların/detayın kullandığı stabil kod — C9 StockAlert
/// deseni; ürün yeniden aktarılsa da kod değişmez). VariantId bilgi amaçlı (detaydan
/// favorilenirse seçili varyant). Listeleme canlı katalogla birleşir — silinen ürünün
/// favorisi listede görünmez.
/// </summary>
public class Favorite : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public Guid MemberId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public Guid? VariantId { get; set; }

    /// <summary>Favorilenen renk (attribute value) — null: renk ekseni olmayan ürün/renksiz
    /// favori. 2026-07-16: favori artık ürün+renk bazlıdır (tüm renkler birden işaretlenmez).</summary>
    public Guid? ColorValueId { get; set; }
}
