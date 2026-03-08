using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Core.Domain.Entities;

public class FirmPlatform : BaseEntity
{
    public Guid FirmId { get; set; }
    public Guid PlatformTypeId { get; set; }
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public Dictionary<string, object> Credentials { get; set; } = new();
    public Dictionary<string, object> Settings { get; set; } = new();
    public string? PriceType { get; set; }
    public decimal? PriceMultiplier { get; set; }
    public Guid? InvoiceSeriesId { get; set; }
    public bool IsActive { get; set; } = true;

    public Firm Firm { get; set; } = null!;
    public PlatformType PlatformType { get; set; } = null!;
}
