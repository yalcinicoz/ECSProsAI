using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Core.Domain.Entities;

/// <summary>Kargo firmasının (PTT gibi) firmaya tahsis ettiği barkod aralığı —
/// range stratejisinde kodlar buradan atomik tahsis edilir. Tahsis edilen barkod
/// hiçbir durumda havuza geri dönmez (karar 2026-07-19); aralık tükenince açık
/// hata verilir, sessiz fallback yoktur.</summary>
public class CargoBarcodeRange : BaseEntity
{
    public Guid FirmPlatformIntegrationId { get; set; }
    public long RangeStart { get; set; }
    public long RangeEnd { get; set; }
    public long NextValue { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? ExhaustedAt { get; set; }

    public FirmPlatformIntegration FirmPlatformIntegration { get; set; } = null!;
}
