using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Core.Domain.Entities;

/// <summary>
/// core.core_label_templates — kullanıcı tasarımlı etiket şablonu (tedarik T3, K7:
/// docs/urun-tedarik-is-akisi.md §2.5). Sabit format yoktur; kağıt ölçüsü (mm) ve elemanlar
/// (barkod/alan/serbest metin/fiyat; konum-boyut mm, yazı pt) kullanıcı tarafından tasarlanır.
/// TargetType=product → ürün etiketi (varyant verisiyle), bin → birim/raf etiketi.
/// </summary>
public class LabelTemplate : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>product | bin</summary>
    public string TargetType { get; set; } = "product";
    public decimal WidthMm { get; set; }
    public decimal HeightMm { get; set; }
    /// <summary>Eleman listesi JSON (camelCase): [{type, field?, text?, x, y, w, h, fontPt, align, bold}]
    /// type: barcode|field|text|price; field: name|sku|barcode|color|size|code (bin: code|barcode|section|warehouse)</summary>
    public string ElementsJson { get; set; } = "[]";
    /// <summary>Hedef tip başına tek varsayılan (ayrıştırma ekranı ilk bunu seçer).</summary>
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}
