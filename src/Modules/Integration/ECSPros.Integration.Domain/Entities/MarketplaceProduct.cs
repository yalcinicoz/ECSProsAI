using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Integration.Domain.Entities;

public class MarketplaceProduct : BaseEntity
{
    public Guid FirmIntegrationId { get; set; }
    public Guid VariantId { get; set; }
    public string ExternalId { get; set; } = string.Empty;      // pazaryerindeki ID
    public string? ExternalBarcode { get; set; }
    public string SyncStatus { get; set; } = "pending";         // pending, synced, failed, deactivated
    public DateTime? LastSyncedAt { get; set; }
    public string? LastSyncError { get; set; }
    public decimal? MarketplacePrice { get; set; }
    public int? MarketplaceStock { get; set; }
    public DateTime? StockSyncedAt { get; set; }
}
