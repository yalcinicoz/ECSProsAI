using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Integration.Domain.Entities;

/// <summary>
/// Bizim özellik değeri → pazaryeri değeri eşlemesi (§2.3). Pazaryeri değerleri kategori+özellik
/// kapsamlıdır (aynı özellik farklı kategoride farklı değer seti taşıyabilir), eşleme de öyle.
/// Hedefin üç kimliği de tutulur; gönderimde hangisinin kullanılacağını özelliğin ValueMode'u
/// belirler (id | code | literal).
/// </summary>
public class MarketplaceValueMapping : BaseEntity
{
    public string Marketplace { get; set; } = string.Empty;
    public string MpCategoryExternalId { get; set; } = string.Empty;
    public string MpAttributeExternalId { get; set; } = string.Empty;
    public Guid AttributeValueId { get; set; }                   // definition.attribute_values (bizim değer)
    public Guid? FirmPlatformId { get; set; }

    public string? TargetExternalId { get; set; }
    public string? TargetCode { get; set; }
    public string TargetValue { get; set; } = string.Empty;      // snapshot metin

    public string Status { get; set; } = "active";               // active | broken | needs_review
    public string? StatusNote { get; set; }
}
