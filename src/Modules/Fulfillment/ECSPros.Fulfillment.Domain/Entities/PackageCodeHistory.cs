using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Fulfillment.Domain.Entities;

/// <summary>Paket no / kargo kodu değişiklik izi — paket güncellenince eski değerler
/// buraya yazılır; hiçbir kod havuza geri dönmez (karar 2026-07-19). ChangedAt/ChangedBy
/// için BaseEntity.CreatedAt/CreatedBy kullanılır.</summary>
public class PackageCodeHistory : BaseEntity
{
    public Guid PackageId { get; set; }
    public string? OldPackageNumber { get; set; }
    public string? OldCargoIntegrationCode { get; set; }
    /// <summary>merge, repack, cargo_change… — işlemin türü.</summary>
    public string ChangeType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
