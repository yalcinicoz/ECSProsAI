using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Integration.Domain.Entities;

/// <summary>
/// Pazaryerine özel ürün özellik değeri (§2.4, K6): bizde karşılığı olmayan/olması anlamsız
/// zorunlu özellikler için personelin tamamlama ekranından girdiği değerler. Kendi kataloğa
/// HİÇBİR ŞEY yazılmaz. Gönderimde kaynak önceliği: ürün-özel değer > değer eşlemesi >
/// sabit değer > serbest geçirme. Pazaryeri değer kimlikleri kategoriye bağlı olduğundan
/// (aynı özelliğin değer listesi kategori başına farklı) kategori kapsamı da tutulur —
/// ürünün çözülen kategorisi değişirse değer geçersiz sayılır, readiness yeniden ister.
/// </summary>
public class MarketplaceProductAttributeValue : BaseEntity
{
    public Guid ProductId { get; set; }
    public string Marketplace { get; set; } = string.Empty;
    public Guid? FirmPlatformId { get; set; }

    public string MpCategoryExternalId { get; set; } = string.Empty;
    public string MpAttributeExternalId { get; set; } = string.Empty;
    public string MpAttributeName { get; set; } = string.Empty;   // snapshot

    public string? ValueExternalId { get; set; }                  // liste özelliği: seçilen değerin kimliği
    public string? ValueCode { get; set; }
    public string? ValueText { get; set; }                        // serbest giriş / görünen metin
}
