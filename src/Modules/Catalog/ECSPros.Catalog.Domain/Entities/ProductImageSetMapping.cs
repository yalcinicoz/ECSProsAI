using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

public class ProductImageSetMapping : BaseEntity
{
    public Guid ProductId { get; set; }
    public Guid ForSetId { get; set; }
    public Guid UseSetId { get; set; }

    public Product Product { get; set; } = null!;
    public ImageSet ForSet { get; set; } = null!;
    public ImageSet UseSet { get; set; } = null!;
}
