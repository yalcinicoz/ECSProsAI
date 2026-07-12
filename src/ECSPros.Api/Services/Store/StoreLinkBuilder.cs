using ECSPros.Core.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Api.Services.Store;

/// <summary>
/// H8: e-posta içine girecek MUTLAK site linki — HTTP bağlamı yok (event handler /
/// arka plan job'ı), host `Store:Hosts` config haritasının tersinden bulunur
/// (host → platform kodu ⇒ platform kodu → host). Platforma host tanımlı değilse
/// null döner — çağıran linksiz şablon basar (yanlış host'a link üretmekten iyidir).
/// </summary>
public interface IStoreLinkBuilder
{
    Task<string?> BuildAsync(Guid firmPlatformId, string path, CancellationToken ct = default);
}

public class StoreLinkBuilder(
    ICoreDbContext coreDb,
    IConfiguration configuration,
    IMemoryCache cache) : IStoreLinkBuilder
{
    public async Task<string?> BuildAsync(Guid firmPlatformId, string path, CancellationToken ct = default)
    {
        var kod = await cache.GetOrCreateAsync($"store-link:platform-code:{firmPlatformId}", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return await coreDb.FirmPlatforms
                .Where(p => p.Id == firmPlatformId)
                .Select(p => p.Code)
                .FirstOrDefaultAsync(ct);
        });
        if (string.IsNullOrWhiteSpace(kod)) return null;

        var host = configuration.GetSection("Store:Hosts").GetChildren()
            .FirstOrDefault(c => string.Equals(c.Value, kod, StringComparison.OrdinalIgnoreCase))?.Key;
        if (string.IsNullOrWhiteSpace(host)) return null;

        return $"https://{host}{(path.StartsWith('/') ? path : "/" + path)}";
    }
}
