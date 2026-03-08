namespace ECSPros.Catalog.Domain.Entities;

public class VariantPriceHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VariantId { get; set; }
    public Guid? FirmPlatformId { get; set; }
    public string PriceType { get; set; } = string.Empty; // base_price, base_cost, platform_price
    public decimal OldValue { get; set; }
    public decimal NewValue { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public Guid ChangedBy { get; set; }
    public string? ChangeReason { get; set; }
    public bool IsDeleted { get; set; } = false;

    public ProductVariant Variant { get; set; } = null!;
}
