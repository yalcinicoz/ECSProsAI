using ECSPros.Core.Application.Services;
using ECSPros.Shared.Contracts.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Core.Infrastructure.Services;

/// <summary>
/// Kanalın etkin yetenek setini çözer: PlatformType.CapabilitiesJson (yoksa koda göre varsayılan)
/// + FirmPlatform.CapabilityOverridesJson (yalnız ezilebilir anahtarlar). 2 dk süreç-içi önbellek
/// (Redis bağımlılığı yok — CLAUDE.md Redis kuralı).
/// </summary>
public sealed class ChannelCapabilityResolver : IChannelCapabilityResolver
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);
    private readonly ICoreDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ECSPros.Shared.Contracts.ICacheBustPublisher _cacheBust;

    public ChannelCapabilityResolver(ICoreDbContext db, IMemoryCache cache,
        ECSPros.Shared.Contracts.ICacheBustPublisher cacheBust)
    {
        _db = db;
        _cache = cache;
        _cacheBust = cacheBust;
    }

    private static string Key(Guid id) => $"chcaps:{id:N}";

    public async Task<ChannelCapabilities> GetAsync(Guid firmPlatformId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(Key(firmPlatformId), out ChannelCapabilities? cached) && cached is not null)
            return cached;

        var row = await _db.FirmPlatforms.AsNoTracking()
            .Where(fp => fp.Id == firmPlatformId)
            .Select(fp => new { fp.CapabilityOverridesJson, fp.PlatformType.Code, fp.PlatformType.IsMarketplace, fp.PlatformType.CapabilitiesJson })
            .FirstOrDefaultAsync(ct);

        var caps = row is null
            ? new ChannelCapabilities()
            : (ChannelCapabilities.Parse(row.CapabilitiesJson) ?? ChannelCapabilities.DefaultsFor(row.Code, row.IsMarketplace))
                .WithOverrides(row.CapabilityOverridesJson);

        _cache.Set(Key(firmPlatformId), caps, Ttl);
        return caps;
    }

    public ChannelCapabilities DefaultsFor(string platformTypeCode) => ChannelCapabilities.DefaultsFor(platformTypeCode);

    public void Invalidate(Guid? firmPlatformId = null)
    {
        // FAZ 10 / A9: yerel silme + diğer düğümlere yayın (pub/sub; Redis'siz yalnız yerel).
        if (firmPlatformId.HasValue) _cacheBust.Bust(Key(firmPlatformId.Value));
        // Tümünü temizleme gerekirse TTL (2 dk) yeterli — tip düzeyi değişiklik nadir.
    }
}
