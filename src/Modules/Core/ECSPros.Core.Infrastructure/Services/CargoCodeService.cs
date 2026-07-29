using ECSPros.Core.Infrastructure.Persistence;
using ECSPros.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Infrastructure.Services;

/// <summary>Taşıyıcıya özel kargo kodu üretimi (F3). Strateji ve kurallar
/// definition.integration_services'te, tahsisli aralıklar core_cargo_barcode_ranges'te.
/// range tahsisi atomiktir (UPDATE...RETURNING); aralık tükenince açık hata döner,
/// sessiz fallback yoktur. Hiçbir kod havuza geri dönmez.</summary>
public class CargoCodeService : ICargoCodeService
{
    private readonly CoreDbContext _db;

    public CargoCodeService(CoreDbContext db) => _db = db;

    public async Task<CargoCodeResult> GenerateAsync(
        Guid firmPlatformIntegrationId, string packageNumber, CancellationToken ct = default)
    {
        var entegrasyon = await _db.FirmPlatformIntegrations.AsNoTracking()
            .Include(i => i.IntegrationService)
            .FirstOrDefaultAsync(i => i.Id == firmPlatformIntegrationId, ct);

        if (entegrasyon is null)
            return CargoCodeResult.Failure("Kargo entegrasyonu bulunamadı.");
        if (!entegrasyon.IsActive)
            return CargoCodeResult.Failure("Kargo entegrasyonu pasif durumda.");

        var servis = entegrasyon.IntegrationService;
        if (servis.ServiceType != "cargo")
            return CargoCodeResult.Failure("Seçilen entegrasyon bir kargo servisi değil.");

        var strateji = string.IsNullOrWhiteSpace(servis.CargoCodeStrategy)
            ? "free"
            : servis.CargoCodeStrategy!.Trim().ToLowerInvariant();

        switch (strateji)
        {
            case "external":
                return CargoCodeResult.Failure(
                    $"'{ServisAdi(servis.NameI18n, servis.Code)}' kargo kodunu kendisi verir; kod üretilmez, dış kod girilmelidir.");

            case "range":
                return await AralikTanAsync(entegrasyon.Id, servis, ct);

            case "free":
            case "pattern":
            {
                var onek = entegrasyon.Settings.TryGetValue("cargoCodePrefix", out var p)
                    ? p?.ToString() ?? string.Empty
                    : string.Empty;
                var kod = onek + packageNumber;
                return Dogrula(kod, servis) is { } hata
                    ? CargoCodeResult.Failure(hata)
                    : CargoCodeResult.Success(kod);
            }

            default:
                return CargoCodeResult.Failure(
                    $"Bilinmeyen kargo kod stratejisi: '{strateji}' (beklenen: free, pattern, range, external).");
        }
    }

    private sealed class RangeSlot
    {
        public long Value { get; set; }
        public long RangeEnd { get; set; }
        public Guid RangeId { get; set; }
    }

    private async Task<CargoCodeResult> AralikTanAsync(
        Guid integrationId, Core.Domain.Entities.IntegrationService servis, CancellationToken ct)
    {
        // En eski aktif, tükenmemiş aralıktan atomik tahsis. NextValue geri alınmaz.
        var rows = await _db.Database.SqlQuery<RangeSlot>($"""
            WITH secilen AS (
                SELECT "Id" FROM core.core_cargo_barcode_ranges
                WHERE "FirmPlatformIntegrationId" = {integrationId}
                  AND "IsActive" = true AND "IsDeleted" = false
                  AND "NextValue" <= "RangeEnd"
                ORDER BY "RangeStart"
                LIMIT 1
            ), taken AS (
                UPDATE core.core_cargo_barcode_ranges r
                SET "NextValue" = r."NextValue" + 1,
                    "UpdatedAt" = timezone('utc', now())
                FROM secilen s
                WHERE r."Id" = s."Id" AND r."NextValue" <= r."RangeEnd"
                RETURNING r."NextValue" - 1 AS "Value", r."RangeEnd", r."Id" AS "RangeId"
            )
            SELECT "Value", "RangeEnd", "RangeId" FROM taken
            """).ToListAsync(ct);

        var slot = rows.SingleOrDefault();
        if (slot is null)
            return CargoCodeResult.Failure(
                $"'{ServisAdi(servis.NameI18n, servis.Code)}' için tahsisli barkod aralığı tükendi veya tanımlı değil; yeni aralık tanımlayın.");

        // Aralığın son değeri kullanıldıysa tükendi olarak işaretle (bilgi amaçlı)
        if (slot.Value == slot.RangeEnd)
            await _db.Database.ExecuteSqlAsync($"""
                UPDATE core.core_cargo_barcode_ranges
                SET "ExhaustedAt" = timezone('utc', now())
                WHERE "Id" = {slot.RangeId} AND "ExhaustedAt" IS NULL
                """, ct);

        // PTT tarzı sabit uzunluk: aralık sonunun basamak sayısına sola sıfır dolgu
        var kod = slot.Value.ToString().PadLeft(slot.RangeEnd.ToString().Length, '0');
        return Dogrula(kod, servis) is { } hata
            ? CargoCodeResult.Failure(hata)
            : CargoCodeResult.Success(kod);
    }

    private static string? Dogrula(string kod, Core.Domain.Entities.IntegrationService servis)
    {
        if (servis.CargoCodeMinLength is { } min && kod.Length < min)
            return $"Üretilen kargo kodu '{kod}' taşıyıcının en az {min} karakter kuralına uymuyor.";
        if (servis.CargoCodeMaxLength is { } max && kod.Length > max)
            return $"Üretilen kargo kodu '{kod}' taşıyıcının en çok {max} karakter kuralını aşıyor.";

        var charset = servis.CargoCodeCharset?.Trim().ToLowerInvariant();
        if (charset == "numeric" && !kod.All(char.IsAsciiDigit))
            return $"Üretilen kargo kodu '{kod}' yalnız rakam kuralına uymuyor.";
        if (charset == "alnum" && !kod.All(char.IsAsciiLetterOrDigit))
            return $"Üretilen kargo kodu '{kod}' harf/rakam dışı karakter içeriyor.";

        return null;
    }

    private static string ServisAdi(Dictionary<string, string> nameI18n, string code) =>
        nameI18n.TryGetValue("tr", out var ad) ? ad : code;
}
