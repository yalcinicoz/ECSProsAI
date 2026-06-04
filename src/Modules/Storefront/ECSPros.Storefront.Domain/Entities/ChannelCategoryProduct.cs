namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// Manuel veya sabit ürün ataması. FillType=manual/mixed kategorilerde kullanılır.
/// IsExcluded=true olduğunda filtre sonucundan çıkarılır (mixed mod).
/// </summary>
public class ChannelCategoryProduct
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChannelCategoryId { get; set; }
    public Guid ProductId { get; set; }
    public int SortOrder { get; set; }
    public bool IsExcluded { get; set; }

    public ChannelCategory ChannelCategory { get; set; } = null!;
}
