namespace ECSPros.Fulfillment.Application.Services;

/// <summary>Kanala özel, siparişten bağımsız seriden paket numarası üretir
/// (karar 2026-07-19). Seri yoksa kanal kodundan türetilen önekle varsayılan seri
/// açılır. Üretim atomiktir; sayaç asla geri alınmaz.</summary>
public interface IPackageNumberService
{
    Task<string> GenerateAsync(Guid firmPlatformId, CancellationToken ct = default);
}
