using System.Text.Json;
using ECSPros.Promotion.Domain.Entities;
using ECSPros.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Services;

/// <summary>
/// F2: <see cref="IProductCampaignResolver"/> uygulaması. Platform için aktif kampanyaları öncelik
/// sırasına göre çeker; her ürün için kapsam (FillType all / materyalize edilmiş ürün) + dışlama
/// denetiminden geçen EN YÜKSEK öncelikli kampanyayı seçer. discount tipinde ürün-bazlı fiyat
/// (applyTo=selected + koşulsuz + percent/amount) hesaplanabilir; diğer durumlar "cart_only".
/// </summary>
public class ProductCampaignResolver(IPromotionDbContext db) : IProductCampaignResolver
{
    public async Task<Dictionary<Guid, ProductCampaignInfo>> ResolveForProductsAsync(
        Guid firmPlatformId, IReadOnlyCollection<Guid> productIds, CancellationToken ct = default)
    {
        var result = new Dictionary<Guid, ProductCampaignInfo>();
        if (productIds.Count == 0 || firmPlatformId == Guid.Empty) return result;

        var now = DateTime.UtcNow;
        var campaigns = await db.Campaigns.AsNoTracking()
            .Include(c => c.CampaignType)
            .Include(c => c.Products)
            .Include(c => c.Exclusions)
            .Where(c => c.FirmPlatformId == firmPlatformId && c.IsActive
                     && c.StartsAt <= now && (c.EndsAt == null || c.EndsAt >= now))
            .OrderByDescending(c => c.Priority)
            .ToListAsync(ct);
        if (campaigns.Count == 0) return result;

        // Materyalize kapsam ürünleri + dışlamalar kampanya başına küme
        var scopeByCampaign = campaigns.ToDictionary(
            c => c.Id, c => c.Products.Where(p => p.ProductId.HasValue).Select(p => p.ProductId!.Value).ToHashSet());
        var exclByCampaign = campaigns.ToDictionary(
            c => c.Id, c => c.Exclusions.Where(e => e.ProductId.HasValue).Select(e => e.ProductId!.Value).ToHashSet());

        foreach (var pid in productIds)
        {
            foreach (var c in campaigns) // öncelik azalan
            {
                var inScope = c.FillType == "all" || scopeByCampaign[c.Id].Contains(pid);
                if (!inScope) continue;
                if (exclByCampaign[c.Id].Contains(pid)) continue;

                result[pid] = BuildInfo(c);
                break; // en yüksek öncelikli kazandı
            }
        }
        return result;
    }

    private static ProductCampaignInfo BuildInfo(Campaign c)
    {
        var name = c.NameI18n.TryGetValue("tr", out var tr) ? tr
            : c.NameI18n.Values.FirstOrDefault() ?? c.Code;
        var typeCode = c.CampaignType?.Code ?? "";

        if (typeCode == "discount")
        {
            var applyTo = Str(c.Settings, "applyTo");
            var cond = Str(c.Settings, "conditionType");
            var benefitType = Str(c.Settings, "benefitType");
            var val = Dec(c.Settings, "benefitValue");

            // Ürün-bazlı gösterim: kapsamdaki ürünlere, koşulsuz, yüzde/tutar.
            if (applyTo == "selected"
                && (cond is "none" or "" or null)
                && (benefitType is "percent" or "amount")
                && val > 0)
                return new ProductCampaignInfo(c.Id, c.Code, name, c.BadgeLabel, typeCode,
                    benefitType, val, DecN(c.Settings, "maxDiscountAmount"));
        }

        // Sepet-bağımlı (buy_x_get_y/bundle/free_shipping/koşullu discount…): yalnız rozet.
        return new ProductCampaignInfo(c.Id, c.Code, name, c.BadgeLabel, typeCode, "cart_only", 0m, null);
    }

    // ── jsonb (JsonElement) duyarlı değer çıkarıcılar ──
    private static string? Str(Dictionary<string, object> s, string key) => s.TryGetValue(key, out var v) ? v switch
    {
        string str => str,
        JsonElement je => je.ValueKind == JsonValueKind.String ? je.GetString() : je.ToString(),
        null => null,
        _ => v.ToString()
    } : null;

    private static decimal Dec(Dictionary<string, object> s, string key) => DecN(s, key) ?? 0m;

    private static decimal? DecN(Dictionary<string, object> s, string key)
    {
        if (!s.TryGetValue(key, out var v)) return null;
        return v switch
        {
            decimal d => d,
            double db => (decimal)db,
            long l => l,
            int i => i,
            string str => decimal.TryParse(str, out var p) ? p : null,
            JsonElement je => je.ValueKind == JsonValueKind.Number ? je.GetDecimal()
                : je.ValueKind == JsonValueKind.String && decimal.TryParse(je.GetString(), out var jp) ? jp : null,
            _ => null
        };
    }
}
