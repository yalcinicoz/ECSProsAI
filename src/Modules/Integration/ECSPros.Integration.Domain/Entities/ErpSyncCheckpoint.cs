using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Integration.Domain.Entities;

/// <summary>ERP kaynak senkronunun başarılı son su işaretini kalıcı tutar.</summary>
public sealed class ErpSyncCheckpoint : BaseEntity
{
    public string Slice { get; set; } = string.Empty;
    public DateTime WatermarkUtc { get; set; }
    public string? LastError { get; set; }
}
