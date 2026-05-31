using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// Belirli bir kanalda (marketplace, bayi vb.) açıkça aktif edilen ürünler.
/// Navigasyon ağacına gerek olmayan kanallar (Trendyol, bayiler) için kullanılır.
/// </summary>
public class ChannelProduct : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public Guid ProductId { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
