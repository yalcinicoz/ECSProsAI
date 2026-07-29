using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Integration.Domain.Entities;

/// <summary>
/// Pazaryeri kategori-özelliği → bizim özellik tipi eşlemesi (§2.3). Karşı tarafta özellikler
/// kategoriye bağlı olduğundan eşleme de (marketplace, mpCategory) kapsamındadır.
/// </summary>
public class MarketplaceAttributeMapping : BaseEntity
{
    public string Marketplace { get; set; } = string.Empty;
    public string MpCategoryExternalId { get; set; } = string.Empty;
    public string MpAttributeExternalId { get; set; } = string.Empty;
    public string MpAttributeName { get; set; } = string.Empty;  // snapshot
    public Guid? FirmPlatformId { get; set; }

    /// <summary>map_values: değer eşlemesinden · pass_literal: bizim değer metni aynen ·
    /// fixed_value: her üründe sabit değer.</summary>
    public string Strategy { get; set; } = "map_values";
    public Guid? AttributeTypeId { get; set; }                   // definition.attribute_types (map_values/pass_literal)
    public string? FixedValue { get; set; }                      // fixed_value stratejisinde

    public string Status { get; set; } = "active";               // active | broken | needs_review
    public string? StatusNote { get; set; }
}
