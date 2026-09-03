using ECSPros.Integration.Application.Services;
using ECSPros.Integration.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.LegacyImport;

public sealed record LegacyImportCheckpointValue(DateTime WatermarkUtc, long LastSourceId);

public interface ILegacyImportCheckpointStore
{
    Task<LegacyImportCheckpointValue?> GetAsync(string slice, int platformId, CancellationToken ct);
    Task SaveSuccessAsync(string slice, int platformId, DateTime watermarkUtc, long lastSourceId, CancellationToken ct);
    Task SaveErrorAsync(string slice, int platformId, string error, CancellationToken ct);
}

public sealed class LegacyImportCheckpointStore(IIntegrationDbContext db) : ILegacyImportCheckpointStore
{
    public async Task<LegacyImportCheckpointValue?> GetAsync(string slice, int platformId, CancellationToken ct)
    {
        var row = await db.LegacyImportCheckpoints.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Slice == slice && x.PlatformId == platformId, ct);
        return row is null ? null : new(row.WatermarkUtc, row.LastSourceId);
    }

    public async Task SaveSuccessAsync(
        string slice, int platformId, DateTime watermarkUtc, long lastSourceId, CancellationToken ct)
    {
        var row = await GetTrackedAsync(slice, platformId, ct);
        row.WatermarkUtc = watermarkUtc.Kind == DateTimeKind.Utc ? watermarkUtc : watermarkUtc.ToUniversalTime();
        row.LastSourceId = lastSourceId;
        row.LastError = null;
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveErrorAsync(string slice, int platformId, string error, CancellationToken ct)
    {
        var row = await GetTrackedAsync(slice, platformId, ct);
        row.LastError = error.Length <= 2000 ? error : error[..2000];
        await db.SaveChangesAsync(ct);
    }

    private async Task<LegacyImportCheckpoint> GetTrackedAsync(string slice, int platformId, CancellationToken ct)
    {
        var row = await db.LegacyImportCheckpoints
            .SingleOrDefaultAsync(x => x.Slice == slice && x.PlatformId == platformId, ct);
        if (row is not null) return row;

        row = new LegacyImportCheckpoint
        {
            Slice = slice,
            PlatformId = platformId,
            WatermarkUtc = DateTime.UnixEpoch
        };
        db.LegacyImportCheckpoints.Add(row);
        return row;
    }
}
