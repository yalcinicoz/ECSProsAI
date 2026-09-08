using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

public class CartItem : BaseEntity
{
    public Guid CartId { get; set; }
    public Guid VariantId { get; set; }
    public int Quantity { get; set; }
    public decimal AddedPrice { get; set; }
    /// <summary>
    /// Sepete eklendiği andaki KART fiyatı (kanal/base min + etkin kampanya) — push: cart_price_drop eşiği (2026-09-08).
    /// <see cref="AddedPrice"/> kampanya ÖNCESİ birim fiyattır (sepet kampanyayı canlı uygular), bu yüzden düşüş karşılaştırması
    /// için ayrı taban tutulur. Null kayıtlarda ilk taramada dolar (Favorite.PriceAtAdd kalıbı) — o ana kadarki düşüş kaçar,
    /// yanlış bildirim üretilmez.
    /// </summary>
    public decimal? EffectivePriceAtAdd { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public bool IsAvailable { get; set; } = true;
    public int AvailableQuantity { get; set; } = 0;
    public DateTime LastCheckedAt { get; set; } = DateTime.UtcNow;

    public Cart Cart { get; set; } = null!;
}
