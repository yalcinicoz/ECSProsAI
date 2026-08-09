namespace ECSPros.Order.Application.Services;

/// <summary>OP2: kanal (FirmPlatformId) → firma çözümü — Core modülüne proje referansı
/// vermeden (raw SQL reader kalıbı). Otomatik fatura serisi seçimi için.</summary>
public interface IFirmResolver
{
    Task<Guid?> GetFirmIdAsync(Guid firmPlatformId, CancellationToken ct = default);
}
