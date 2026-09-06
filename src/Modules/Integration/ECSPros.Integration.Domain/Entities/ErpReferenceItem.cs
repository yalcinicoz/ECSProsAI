using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Integration.Domain.Entities;

/// <summary>
/// ERP sözlüğü (docs/erp-esleme-plani.md §2.1, EM0 — 2026-09-06): ERP tarafındaki grup / varyant ekseni /
/// özellik tipi / özellik değeri / tedarikçi / renk kayıtları. marketplace_ref'in FİRMAYA ÖZEL karşılığı —
/// merkezî dağıtılmaz. Anahtar hedef sistem dizesi ("erp:nebim") — eşleme tablolarındaki Marketplace kolonuyla
/// aynı anahtar; IntegrationId yalnız köken (hangi sözleşmeden okundu). Worker doldurur, elle ekleme serbest.
/// </summary>
public class ErpReferenceItem : BaseEntity
{
    /// <summary>"erp:&lt;servis kodu&gt;" — marketplace_*_mappings.Marketplace ile aynı anahtar.</summary>
    public string TargetSystem { get; set; } = string.Empty;
    public Guid? IntegrationId { get; set; }
    /// <summary>product_group | variant_axis | attribute_type | attribute_value | supplier | color</summary>
    public string Kind { get; set; } = ErpReferenceKinds.ProductGroup;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>attribute_value → ait olduğu attribute_type kodu; hiyerarşik grup → üst grup kodu.</summary>
    public string? ParentCode { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    /// <summary>manual | sync</summary>
    public string Source { get; set; } = "manual";
    public string? RawJson { get; set; }
}

public static class ErpReferenceKinds
{
    public const string ProductGroup = "product_group";
    public const string VariantAxis = "variant_axis";
    public const string AttributeType = "attribute_type";
    public const string AttributeValue = "attribute_value";
    public const string Supplier = "supplier";
    public const string Color = "color";
    public static readonly string[] All = [ProductGroup, VariantAxis, AttributeType, AttributeValue, Supplier, Color];
    public static bool IsValid(string? k) => k is not null && All.Contains(k);
}

/// <summary>Eşleme hedef sistemi anahtarı: pazaryeri kodu ("trendyol") ya da ERP ("erp:nebim").</summary>
public static class MappingTargets
{
    public const string ErpPrefix = "erp:";
    public static bool IsErp(string? target) => target is not null && target.StartsWith(ErpPrefix, StringComparison.OrdinalIgnoreCase);
    public static string Erp(string serviceCode) => ErpPrefix + serviceCode.Trim().ToLowerInvariant();
}
