using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// 2026-07-17: sepetten çıkarılan ürün geçmişi — sepet sayfasındaki "Önceden Eklediklerim"
/// (ms-sepet-onceden) bölümünün üyeye bağlı kalıcı kaynağı. Üye+platform+varyant başına
/// tek kayıt (yeniden silinince tarih tazelenir); üye başına son 12 kayıt tutulur.
/// Ad/görsel/fiyat silme anındaki snapshot'tır (sepet kaleminin görünen değerleri).
/// </summary>
public class CartRemovedItem : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public Guid MemberId { get; set; }
    public Guid VariantId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public string CurrencyCode { get; set; } = "TRY";
}
