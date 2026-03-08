using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

public class ProductUnit : BaseEntity
{
    public Guid VariantId { get; set; }
    public string UnitType { get; set; } = "piece"; // piece, dozen, box, carton, pallet
    public Dictionary<string, string> UnitNameI18n { get; set; } = new();
    public int PiecesPerUnit { get; set; } = 1;
    public bool IsDefault { get; set; } = false;
    public int MinOrderQuantity { get; set; } = 1;
    public decimal? PriceMultiplier { get; set; }

    public ProductVariant Variant { get; set; } = null!;
}
