using System.Text.Json;
using ECSPros.Core.Application.Services;
using ECSPros.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Api.Services.Store;

/// <summary>
/// <see cref="IPaymentOptionsProvider"/> — FirmPlatform.Settings jsonb'sinden okur
/// (panel Kanallar ekranı yazar). Kısa süreli bellek önbelleği: ayar değişimi ~1 dk
/// içinde siteye yansır; checkout başına DB sorgusu binmez.
/// </summary>
public class PaymentOptionsProvider(ICoreDbContext db, IMemoryCache cache) : IPaymentOptionsProvider
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(1);
    private static readonly PaymentOptions Varsayilan = new(PaymentOptions.TumYontemler, 50m, 3000m);

    public async Task<PaymentOptions> GetAsync(Guid firmPlatformId, CancellationToken ct = default)
    {
        if (firmPlatformId == Guid.Empty) return Varsayilan;
        var anahtar = $"payment-options:{firmPlatformId:N}";
        if (cache.TryGetValue(anahtar, out PaymentOptions? hazir) && hazir is not null)
            return hazir;

        var settings = await db.FirmPlatforms.AsNoTracking()
            .Where(p => p.Id == firmPlatformId)
            .Select(p => p.Settings)
            .FirstOrDefaultAsync(ct);

        var sonuc = Coz(settings);
        cache.Set(anahtar, sonuc, CacheTtl);
        return sonuc;
    }

    private static PaymentOptions Coz(Dictionary<string, object>? settings)
    {
        if (settings is null) return Varsayilan;

        var yontemler = PaymentOptions.TumYontemler;
        if (settings.TryGetValue("paymentMethods", out var ymObj)
            && ymObj is JsonElement { ValueKind: JsonValueKind.Array } dizi)
        {
            var secili = dizi.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .Where(PaymentOptions.TumYontemler.Contains)
                .Distinct()
                .ToList();
            // Boş/geçersiz seçim güvenli varsayılana düşer (panel boş kaydı zaten engeller)
            if (secili.Count > 0) yontemler = secili;
        }

        return new PaymentOptions(
            yontemler,
            Sayi(settings, "codServiceFee") ?? Varsayilan.CodServiceFee,
            Sayi(settings, "codMaxOrderTotal") ?? Varsayilan.CodMaxOrderTotal);
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
