using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

public class ProductVariantImage : BaseEntity
{
    public Guid VariantId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;
    public bool IsMain { get; set; } = false;

    public ProductVariant Variant { get; set; } = null!;
}
