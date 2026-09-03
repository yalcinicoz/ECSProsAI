using ECSPros.Api.Services.Legacy;

namespace ECSPros.Api.Services.LegacyImport;

/// <summary>Görseli olmayan ürünleri küçük partilerle MySQL metadata'sından tamamlar.</summary>
public sealed class LegacyMissingImageMetadataImportSlice(
    LegacySyncService sync,
    LegacyReadImportOptions options) : ILegacyCommerceImportSlice
{
    public string Slice => LegacyImportSlices.MissingImages;

    public async Task<LegacyImportSliceReport> RunAsync(CancellationToken ct)
    {
        var report = await sync.SyncMissingImagesFromReadOnlySourceAsync(
            options.ConnectionString, options.PlatformId, options.ImagesDryRun,
            options.MissingImagesBatchSize, ct);
        return new(report.Slice, report.Success, report.DryRun, report.Changed, 0, report.Error);
    }
}
