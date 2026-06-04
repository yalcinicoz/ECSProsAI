namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// Hangi ürün grubunun hangi kanal kategorisinin sorumluluğunda olduğunu belirtir.
/// Coverage kontrolü bu tablo üzerinden yapılır.
/// </summary>
public class ChannelCategoryGroup
{
    public Guid ChannelCategoryId { get; set; }
    public Guid ProductGroupId { get; set; }

    public ChannelCategory ChannelCategory { get; set; } = null!;
}
