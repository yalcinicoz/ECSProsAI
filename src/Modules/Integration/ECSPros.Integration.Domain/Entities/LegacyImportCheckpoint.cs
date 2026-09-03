using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Integration.Domain.Entities;

/// <summary>Legacy MySQL salt-okunur importunun dilim ve platform bazlı başarılı su işareti.</summary>
public sealed class LegacyImportCheckpoint : BaseEntity
{
    public string Slice { get; set; } = string.Empty;
    public int PlatformId { get; set; }
    public DateTime WatermarkUtc { get; set; }
    public long LastSourceId { get; set; }
    public string? LastError { get; set; }
}
