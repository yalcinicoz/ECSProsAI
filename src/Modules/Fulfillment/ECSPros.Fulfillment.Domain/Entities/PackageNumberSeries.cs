using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Fulfillment.Domain.Entities;

/// <summary>Kanala (FirmPlatform) özel, siparişten BAĞIMSIZ paket numarası serisi
/// (karar 2026-07-19). Üretim atomiktir; sayaç asla geri alınmaz.</summary>
public class PackageNumberSeries : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public string Prefix { get; set; } = string.Empty;
    public int PadLength { get; set; } = 6;
    public long NextValue { get; set; } = 1;
    public bool IsActive { get; set; } = true;
}
