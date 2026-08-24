using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Procurement.Domain.Entities;

/// <summary>
/// procurement.missing_card_notices — "kart eksik" bildirimi (K9): ayrıştırma personeli katalogda
/// bulunmayan ürünü BİLDİRİR, kart açmaz. Katalog sorumlusu kartı açınca bildirimi çözer; sayım sonra yapılır.
/// </summary>
public class MissingCardNotice : BaseEntity
{
    public Guid? ReceiptBatchId { get; set; }
    public string DescriptionText { get; set; } = string.Empty;   // aranan metin + personel notu
    /// <summary>open | resolved</summary>
    public string Status { get; set; } = "open";
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedBy { get; set; }
}
