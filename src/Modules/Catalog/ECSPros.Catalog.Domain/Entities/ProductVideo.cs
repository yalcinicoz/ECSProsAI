using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

public class ProductVideo : BaseEntity
{
    public Guid ProductId { get; set; }
    public Guid ImageSetId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? ThumbnailFileName { get; set; }
    public int SortOrder { get; set; } = 0;
    public ProductImageStatus Status { get; set; } = ProductImageStatus.Pending;
    public Guid BatchId { get; set; }
    public DateTime? ArchivedAt { get; set; }

    public Product Product { get; set; } = null!;
    public ImageSet ImageSet { get; set; } = null!;
}
