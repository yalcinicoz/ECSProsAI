using System.Text.Json;
using ECSPros.Promotion.Application.Services.Engine;
using ECSPros.Promotion.Domain.Entities;
using ECSPros.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Services;

/// <summary>
/// F2/F4: <see cref="IProductCampaignResolver"/> uygulaması. Platform için aktif kampanyaları öncelik
/// sırasına göre çeker; her ürün için kapsam (FillType all / materyalize) + dışlama sonrası EN YÜKSEK
/// öncelikli kampanyayı seçer. discount + applyTo=selected + koşulsuz + percent/amount → ürün-bazlı
/// fiyat; diğerleri cart_only (sepet motoru CampaignEngine ile). Her ürünün TEK etkin kampanyası
/// olduğundan çift sayım olmaz.
/// </summary>
public class ProductCampaignResolver(IPromotionDbContext db) : IProductCampaignResolver
{
    private async Task<List<Campaign>> AktifKampanyalarAsync(Guid fp, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return await db.Campaigns.AsNoTracking()
            .Include(c => c.CampaignType)
            .Include(c => c.Products)
            .Include(c => c.Exclusions)
            .Where(c => c.FirmPlatformId == fp && c.IsActive
                     && c.StartsAt <= now && (c.EndsAt == null || c.EndsAt >= now))
            .OrderByDescending(c => c.Priority)
            .ToListAsync(ct);
    }

    /// <summary>Her ürün için en yüksek öncelikli, kapsam+dışlama denetiminden geçen kampanya.</summary>
    private static Dictionary<Guid, Campaign> EtkinKampanya(List<Campaign> campaigns, IEnumerable<Guid> productIds)
    {
        var scope = campaigns.ToDictionary(c => c.Id,
            c => c.Products.Where(p => p.ProductId.HasValue).Select(p => p.ProductId!.Value).ToHashSet());
        var excl = campaigns.ToDictionary(c => c.Id,
            c => c.Exclusions.Where(e => e.ProductId.HasValue).Select(e => e.ProductId!.Value).ToHashSet());

        var result = new Dictionary<Guid, Campaign>();
        foreach (var pid in productIds.Distinct())
            foreach (var c in campaigns) // öncelik azalan
            {
                if (!(c.FillType == "all" || scope[c.Id].Contains(pid))) continue;
                if (excl[c.Id].Contains(pid)) continue;
                result[pid] = c;
                break;
            }
        return result;
    }

    public async Task<Dictionary<Guid, ProductCampaignInfo>> ResolveForProductsAsync(
        Guid firmPlatformId, IReadOnlyCollection<Guid> productIds, CancellationToken ct = default)
    {
        var result = new Dictionary<Guid, ProductCampaignInfo>();
        if (productIds.Count == 0 || firmPlatformId == Guid.Empty) return result;

        var campaigns = await AktifKampanyalarAsync(firmPlatformId, ct);
        if (campaigns.Count == 0) return result;

        foreach (var (pid, c) in EtkinKampanya(campaigns, productIds))
            result[pid] = BuildInfo(c);
        return result;
    }

    public async Task<CartCampaignResult> ResolveCartAsync(
        Guid firmPlatformId, IReadOnlyList<CartCampaignItem> items, CancellationToken ct = default)
    {
        var itemPrices = new Dictionary<Guid, decimal>();
        var applied = new List<AppliedCampaign>();
        decimal cartDiscount = 0m;
        if (items.Count == 0 || firmPlatformId == Guid.Empty)
            return new CartCampaignResult(itemPrices, 0m, applied);

        var campaigns = await AktifKampanyalarAsync(firmPlatformId, ct);
        if (campaigns.Count == 0) return new CartCampaignResult(itemPrices, 0m, applied);

        var winner = EtkinKampanya(campaigns, items.Select(i => i.ProductId));

        // Ürün-bazlı fiyatlar + cart_only kampanyalar için uygulanabilir varyant kümesi
        var cartOnly = new Dictionary<Guid, Campaign>();
        var applicable = new Dictionary<Guid, HashSet<Guid>>();
        var urunBazliTutar = new Dictionary<Guid, (Campaign C, decimal Tutar)>();

        foreach (var it in items)
        {
            if (!winner.TryGetValue(it.ProductId, out var c)) continue;
            var info = BuildInfo(c);
            if (info.BenefitKind is "percent" or "amount"
                && CampaignPricing.EffectivePrice(info, it.UnitPrice) is { } kf)
            {
                itemPrices[it.VariantId] = kf;
                var kazanc = (it.UnitPrice - kf) * it.Quantity;
                var mevcut = urunBazliTutar.GetValueOrDefault(c.Id);
                urunBazliTutar[c.Id] = (c, mevcut.Tutar + kazanc);
            }
            else // cart_only
            {
                cartOnly[c.Id] = c;
                (applicable.TryGetValue(c.Id, out var set) ? set : applicable[c.Id] = new()).Add(it.VariantId);
            }
        }

        // Ürün-bazlı kampanya özetleri (fiyata yansıdı; toplam kazanç bilgi amaçlı)
        foreach (var (_, v) in urunBazliTutar)
            if (v.Tutar > 0)
                applied.Add(new AppliedCampaign(v.C.Code, NameOf(v.C), Math.Round(v.Tutar, 2), "product"));

        // Sepet-seviyesi (cart_only) — CampaignEngine (buy_x_get_y / min_cart …)
        if (cartOnly.Count > 0)
        {
            var cartLines = items
                .Select(i => new CartLineItem(i.VariantId, i.Quantity, itemPrices.GetValueOrDefault(i.VariantId, i.UnitPrice)))
                .ToList();
            foreach (var (cid, c) in cartOnly)
            {
                var line = CampaignEngine.Calculate(c, cartLines, applicable[cid]);
                if (line is { DiscountAmount: > 0 })
                {
                    cartDiscount += line.DiscountAmount;
                    applied.Add(new AppliedCampaign(c.Code, NameOf(c), line.DiscountAmount, "cart"));
                }
            }
        }

        return new CartCampaignResult(itemPrices, Math.Round(cartDiscount, 2), applied);
    }

    private static string NameOf(Campaign c) =>
        c.NameI18n.TryGetValue("tr", out var tr) ? tr : c.NameI18n.Values.FirstOrDefault() ?? c.Code;

    private static ProductCampaignInfo BuildInfo(Campaign c)
    {
        var name = NameOf(c);
        var typeCode = c.CampaignType?.Code ?? "";

        if (typeCode == "discount")
        {
            var applyTo = Str(c.Settings, "applyTo");
            var cond = Str(c.Settings, "conditionType");
            var benefitType = Str(c.Settings, "benefitType");
            var val = Dec(c.Settings, "benefitValue");

            if (applyTo == "selected"
                && (cond is "none" or "" or null)
                && (benefitType is "percent" or "amount")
                && val > 0)
                return new ProductCampaignInfo(c.Id, c.Code, name, c.BadgeLabel, typeCode,
                    benefitType, val, DecN(c.Settings, "maxDiscountAmount"));
        }

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
