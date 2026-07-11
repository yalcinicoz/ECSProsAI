using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// G1: yayın geçmişi kaydı (spec: PublishLog) — her Yayınla ve Rollback denemesi
/// bir satır bırakır (başarısız denemeler dahil; ErrorMessage doluysa yayına
/// geçmemiştir). PreviousVersion rollback izlenebilirliği içindir: hangi versiyondan
/// hangisine geçildi. Admin "Yayın Geçmişi" ekranı (G6/G13) bu tabloyu listeler.
/// </summary>
public class PublishLog : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public int Version { get; set; }
    public int? PreviousVersion { get; set; }
    public Guid? PublishedBy { get; set; }
    public DateTime PublishedAt { get; set; }
    public string Status { get; set; } = "success"; // success | failed | rollback
    public string? ErrorMessage { get; set; }
    public string? Note { get; set; }
}
