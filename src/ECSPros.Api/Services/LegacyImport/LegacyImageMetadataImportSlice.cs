using ECSPros.Api.Services.Legacy;

namespace ECSPros.Api.Services.LegacyImport;

/// <summary>
/// Production MySQL'deki apurunresimleri metadata'sını yeni kataloğa uzlaştırır.
/// Kaynak bağlantı SELECT-only olmalıdır; fiziksel görsel dosyalarına dokunulmaz.
/// </summary>
public sealed class LegacyImageMetadataImportSlice(
    LegacySyncService sync,
    LegacyReadImportOptions options) : ILegacyCommerceImportSlice
{
    public string Slice => LegacyImportSlices.Images;

    public async Task<LegacyImportSliceReport> RunAsync(CancellationToken ct)
    {
        var report = await sync.SyncImagesFromReadOnlySourceAsync(
            options.ConnectionString, options.PlatformId, options.ImagesDryRun, ct);
        return new(report.Slice, report.Success, report.DryRun, report.Changed, 0, report.Error);
    }
}
