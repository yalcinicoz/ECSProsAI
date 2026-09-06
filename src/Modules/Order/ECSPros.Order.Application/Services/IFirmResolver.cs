namespace ECSPros.Order.Application.Services;

/// <summary>Core şemasından salt-okunur kanal/firma/sözleşme bilgisi (raw SQL; modül sınırı korunur).</summary>
public interface IFirmResolver
{
    Task<Guid?> GetFirmIdAsync(Guid firmPlatformId, CancellationToken ct = default);
    Task<ChannelInfo?> GetChannelAsync(Guid firmPlatformId, CancellationToken ct = default);
    Task<IReadOnlyList<ChannelInfo>> GetChannelsAsync(CancellationToken ct = default);
    /// <summary>ServiceType=einvoice firma sözleşmeleri (FirmPlatformId NULL = firma geneli).</summary>
    Task<IReadOnlyList<IntegrationContractInfo>> GetEInvoiceContractsAsync(Guid? firmId = null, CancellationToken ct = default);
}

public sealed record ChannelInfo(Guid Id, Guid FirmId, string Code, string Name, bool IsActive);

public sealed record IntegrationContractInfo(
    Guid Id, Guid FirmId, Guid? FirmPlatformId, string ServiceCode, string ServiceType,
    string? Name, bool IsActive, string Status, bool TestMode = false);
