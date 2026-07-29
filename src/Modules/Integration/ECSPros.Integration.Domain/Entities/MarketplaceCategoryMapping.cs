using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Integration.Domain.Entities;

/// <summary>
/// Ürün grubu → pazaryeri kategorisi eşlemesi (docs/pazaryeri-entegrasyon-veri-yonetimi.md §2.1).
/// Bizim taraf ProductGroup'tur (katalogda hiyerarşik kategori yok; her ürünün tam bir grubu var).
/// Hedef, referans DB'ye FK olmadan (marketplace, externalId) + snapshot ile tutulur — referans DB
/// yeniden kurulsa bile eşleme ekranı kör kalmaz (K3).
/// </summary>
public class MarketplaceCategoryMapping : BaseEntity
{
    public string Marketplace { get; set; } = string.Empty;      // trendyol, hepsiburada, ...
    public Guid ProductGroupId { get; set; }                     // definition.product_groups (yalnız referans)
    public Guid? FirmPlatformId { get; set; }                    // null = firma geneli (v1 hep null)

    public string MappingKind { get; set; } = "direct";          // direct | rules | pool

    // direct: tek hedef
    public string? TargetExternalId { get; set; }
    public string? TargetName { get; set; }                      // snapshot
    public string? TargetPath { get; set; }                      // snapshot

    // rules: sıralı kural listesi (jsonb) — [{order, attributeTypeCode, valueId, valueLabel,
    //   targetExternalId, targetName, targetPath}]; ilk eşleşen kazanır, TargetExternalId
    //   doluysa varsayılan hedef olarak kullanılır.
    // pool: aday hedef listesi (jsonb) — [{externalId, name, path}]; ürün bazlı atama F3'te.
    public string? RulesJson { get; set; }
    public string? PoolJson { get; set; }

    public string Status { get; set; } = "active";               // active | broken | needs_review
    public string? StatusNote { get; set; }                      // sağlık job'ının insan-okur açıklaması
}
