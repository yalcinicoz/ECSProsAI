using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// C9: "Stok gelince haber ver" aboneliği — üye, tükenen bir varyant için bildirim ister.
/// Bildirim GÖNDERİMİ Faz H'de (e-posta/SMS altyapısıyla); o güne dek kayıtlar birikir,
/// stok girişinde Status='notified' + NotifiedAt işlenerek tüketilecek.
/// ProductCode/VariantInfo anlık görüntüdür (varyant silinse de kayıt anlamlı kalır).
/// </summary>
public class StockAlert : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public Guid VariantId { get; set; }
    public Guid MemberId { get; set; }
    public string? Email { get; set; }
    public string? ProductCode { get; set; }
    public string? VariantInfo { get; set; }
    public string Status { get; set; } = "active"; // active | notified | cancelled
    public DateTime? NotifiedAt { get; set; }
}
