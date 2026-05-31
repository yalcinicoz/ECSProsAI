using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

public class ImageSet : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; } = false;
    public Guid? FallbackSetId { get; set; }
    public int SortPriority { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    public ImageSet? FallbackSet { get; set; }
    public ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();
    public ICollection<ProductImageSetMapping> MappingsAsFor { get; set; } = new List<ProductImageSetMapping>();
    public ICollection<ProductImageSetMapping> MappingsAsUse { get; set; } = new List<ProductImageSetMapping>();
}
