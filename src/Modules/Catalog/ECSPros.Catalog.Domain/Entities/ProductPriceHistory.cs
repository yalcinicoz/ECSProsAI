namespace ECSPros.Catalog.Domain.Entities;

public class ProductPriceHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    /// <summary>"base_price" | "base_cost"</summary>
    public string PriceField { get; set; } = string.Empty;
    public decimal? OldValue { get; set; }
    public decimal? NewValue { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public Guid? ChangedBy { get; set; }
    public string? ChangedByName { get; set; }

    public Product Product { get; set; } = null!;
}
