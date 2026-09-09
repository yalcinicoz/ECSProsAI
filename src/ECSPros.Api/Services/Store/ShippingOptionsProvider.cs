using System.Text.Json;
using ECSPros.Core.Application.Services;
using ECSPros.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Api.Services.Store;

/// <summary>
/// <see cref="IShippingOptionsProvider"/> — FirmPlatform.Settings jsonb'sinden okur
/// (panel Kanallar ekranı yazar: shippingFee / freeShippingThreshold). PaymentOptionsProvider
/// ile aynı kalıp: 1 dk bellek önbelleği, ayar değişimi ~1 dk içinde siteye yansır.
/// Ayar yoksa varsayılan 0/0 → kargo ücretsiz (bugünkü davranış korunur).
/// </summary>
public class ShippingOptionsProvider(ICoreDbContext db, IMemoryCache cache) : IShippingOptionsProvider
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(1);

    public async Task<ShippingOptions> GetAsync(Guid firmPlatformId, CancellationToken ct = default)
    {
        if (firmPlatformId == Guid.Empty) return ShippingOptions.Varsayilan;
        var anahtar = $"shipping-options:{firmPlatformId:N}";
        if (cache.TryGetValue(anahtar, out ShippingOptions? hazir) && hazir is not null)
            return hazir;

        var settings = await db.FirmPlatforms.AsNoTracking()
            .Where(p => p.Id == firmPlatformId)
            .Select(p => p.Settings)
            .FirstOrDefaultAsync(ct);

        var sonuc = Coz(settings);
        cache.Set(anahtar, sonuc, CacheTtl);
        return sonuc;
    }

    private static ShippingOptions Coz(Dictionary<string, object>? settings)
    {
        if (settings is null) return ShippingOptions.Varsayilan;
        return new ShippingOptions(
            Sayi(settings, "shippingFee") ?? 0m,
            Sayi(settings, "freeShippingThreshold") ?? 0m);
    }

    private static decimal? Sayi(Dictionary<string, object> settings, string key)
    {
        if (!settings.TryGetValue(key, out var v)) return null;
        return v switch
        {
            JsonElement { ValueKind: JsonValueKind.Number } je => je.GetDecimal() is var d && d >= 0 ? d : null,
            decimal d2 when d2 >= 0 => d2,
            double db when db >= 0 => (decimal)db,
            int i when i >= 0 => i,
            long l when l >= 0 => l,
            _ => null
        };
    }
}
