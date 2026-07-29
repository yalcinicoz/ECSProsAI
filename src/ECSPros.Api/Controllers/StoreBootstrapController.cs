using ECSPros.Api.Services;
using ECSPros.Core.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Api.Controllers;

/// <summary>
/// Mobil/harici istemci açılış ucu: tüm /api/store/* çağrılarında zorunlu olan
/// firmPlatformId burada öğrenilir. SSR bu bilgiyi Host header'ından çözer;
/// host'u olmayan mobil istemci kanal kodunu ?code= ile gönderir (build config'inde
/// sabitlenir), kod verilmezse SSR ile aynı varsayılan (Store:DefaultFirmPlatformCode)
/// döner. Credentials/Settings gibi hassas alanlar bilinçli olarak dışarıda.
/// </summary>
[ApiController]
[Route("api/store/bootstrap")]
public class StoreBootstrapController(
    ICoreDbContext coreDb,
    IStoreContext storeContext,
    IMemoryCache cache) : ControllerBase
{
    private static readonly TimeSpan CacheSuresi = TimeSpan.FromMinutes(5);

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get([FromQuery] string? code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            var varsayilan = await storeContext.GetPlatformAsync(ct);
            if (varsayilan is null)
                return BadRequest(new { success = false, error = "Kanal çözümlenemedi. ?code= parametresi gönderin." });

            var ad = await cache.GetOrCreateAsync($"store-bootstrap-name:{varsayilan.Code}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheSuresi;
                return await coreDb.FirmPlatforms.AsNoTracking()
                    .Where(p => p.Id == varsayilan.Id)
                    .Select(p => p.NameI18n)
                    .FirstOrDefaultAsync(ct);
            });

            return Ok(new
            {
                success = true,
                data = new { firmPlatformId = varsayilan.Id, code = varsayilan.Code, nameI18n = ad },
            });
        }

        var kod = code.Trim().ToLowerInvariant();
        var platform = await cache.GetOrCreateAsync($"store-bootstrap:{kod}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheSuresi;
            return await coreDb.FirmPlatforms.AsNoTracking()
                .Where(p => p.Code == kod && p.IsActive)
                .Select(p => new { p.Id, p.Code, p.NameI18n })
                .FirstOrDefaultAsync(ct);
        });

        if (platform is null)
            return BadRequest(new { success = false, error = $"'{kod}' kodlu aktif kanal bulunamadı." });

        return Ok(new
        {
            success = true,
            data = new { firmPlatformId = platform.Id, code = platform.Code, nameI18n = platform.NameI18n },
        });
    }
}
