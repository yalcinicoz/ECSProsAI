namespace ECSPros.Shared.Contracts;

/// <summary>Kargo entegrasyon kodu üretimi — kural taşıyıcıya özeldir (F3, 2026-07-19):
/// free (serbest), pattern (uzunluk/karakter kurallı), range (tahsisli barkod aralığı,
/// örn. PTT), external (kod dışarıdan gelir, üretim yapılmaz). Üretilen/iptal edilen
/// kod hiçbir durumda havuza geri dönmez.</summary>
public interface ICargoCodeService
{
    /// <summary>Firma kargo entegrasyonunun stratejisine göre kod üretir.
    /// packageNumber, free/pattern stratejilerinde kodun gövdesini oluşturur.</summary>
    Task<CargoCodeResult> GenerateAsync(
        Guid firmPlatformIntegrationId, string packageNumber, CancellationToken ct = default);
}

public record CargoCodeResult(string? Code, string? Error)
{
    public bool IsSuccess => Error is null;
    public static CargoCodeResult Success(string code) => new(code, null);
    public static CargoCodeResult Failure(string error) => new(null, error);
}
