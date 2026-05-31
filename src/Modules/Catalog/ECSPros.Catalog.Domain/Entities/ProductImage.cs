using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

public enum ProductImageStatus
{
    Pending = 0,
    Active = 1,
    Archived = 2,
    Cancelled = 3
}

public class ProductImage : BaseEntity
{
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public Guid ImageSetId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;
    public bool IsProductCover { get; set; } = false;
    public bool IsVariantCover { get; set; } = false;
    public ProductImageStatus Status { get; set; } = ProductImageStatus.Pending;
    public Guid BatchId { get; set; }
    public DateTime? ArchivedAt { get; set; }

    public Product Product { get; set; } = null!;
    public ProductVariant? Variant { get; set; }
    public ImageSet ImageSet { get; set; } = null!;
}
