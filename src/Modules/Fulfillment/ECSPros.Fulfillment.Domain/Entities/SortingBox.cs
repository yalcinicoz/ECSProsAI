using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Fulfillment.Domain.Entities;

/// <summary>
/// OP3: ara ayrıştırma kolisi OTURUMU — koli numarası sanaldır ve görev boyunca yeniden
/// kullanılır (kullanıcı kurgusu): koli masaya alınıp işi bitince oturum kapanır, aynı
/// numara Generation+1 ile boş koli olarak yeniden açılır. Sipariş→koli eşlemesi
/// SortingBin.SortingBoxId üzerindendir (bir siparişin TÜM ürünleri aynı koliye gider).
/// </summary>
public class SortingBox : BaseEntity
{
    public Guid PickingPlanId { get; set; }
    public int BoxNumber { get; set; }
    public int Generation { get; set; } = 1;

    /// <summary>open (dolduruluyor) | taken (paketleme personeli zimmetine aldı) | closed</summary>
    public string Status { get; set; } = "open";

    public Guid? TakenBy { get; set; }
    public DateTime? TakenAt { get; set; }

    /// <summary>OP4: koli masaya bağlandığında (son ayrıştırma) — "Masada (11)" gösterimi.</summary>
    public Guid? StationId { get; set; }
    public int? StationNumber { get; set; }

    public DateTime? ClosedAt { get; set; }
}
