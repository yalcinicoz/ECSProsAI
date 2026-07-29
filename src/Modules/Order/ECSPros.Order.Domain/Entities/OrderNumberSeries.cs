using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

/// <summary>Kanala (FirmPlatform) özel sipariş numarası serisi. Numara üretimi
/// OrderNumberService'te atomik UPDATE...RETURNING ile yapılır; NextValue asla
/// geri alınmaz (iptal edilen numara havuza dönmez — karar 2026-07-19).</summary>
public class OrderNumberSeries : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public string Prefix { get; set; } = string.Empty;
    public int PadLength { get; set; } = 7;
    public long NextValue { get; set; } = 1;
    public bool IsActive { get; set; } = true;
}
