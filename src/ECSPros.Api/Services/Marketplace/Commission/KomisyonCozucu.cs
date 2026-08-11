using ECSPros.Accounts.Application.Services;
using ECSPros.Catalog.Application.Services;
using ECSPros.Promotion.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.Marketplace.Commission;

/// <summary>
/// P3a (2026-08-11): etkin komisyon oranı çözücüsü — K1 beş katmanı TEK yerde:
/// 1) ürün-özel oran (satıcı sözleşmesi × ürün) — en özel, her şeyi ezer
/// 2) kampanya oranı — kalem kampanyalıysa ve (opt-in gerekliyse) satıcı KATILMIŞSA
/// 3) sözleşme × ürün grubu oranı
/// 4) ciro basamağı PUAN ayarı — yalnız 3/5 katmanlarını modifiye eder (K1)
/// 5) platform varsayılanı (ürün grubu)
/// Hakediş satırına hangi katmanın uygulandığı yazılır ("anlaşılır olsun" şartı).
/// Kaynaklar üç modüldedir (Accounts/Catalog/Promotion) — çözücü host'ta yaşar.
/// </summary>
public sealed class KomisyonCozucu(
    IAccountsDbContext accountsDb,
    ICatalogDbContext catalogDb,
    IPromotionDbContext promotionDb)
{
    public sealed record KalemGirdisi(Guid OrderItemId, Guid VariantId, decimal Gross, decimal Discount, Guid? CampaignId);
    public sealed record KalemKarari(
        Guid OrderItemId, decimal Rate, string Layer, decimal CommissionAmount,
        decimal DiscountShareAmount, Guid? CampaignId, int SettlementDelayDays);

    /// <summary>Bir satıcının teslim edilen kalemleri için toplu karar üretir.</summary>
    public async Task<List<KalemKarari>> CozAsync(
        Guid supplierAccountId, List<KalemGirdisi> kalemler, DateTime deliveredAt, CancellationToken ct)
    {
        var sonuc = new List<KalemKarari>();
        if (kalemler.Count == 0) return sonuc;

        var contract = await accountsDb.SupplierContracts.AsNoTracking()
            .Include(c => c.GroupRates).Include(c => c.ProductRates).Include(c => c.TurnoverTiers)
            .FirstOrDefaultAsync(c => c.CurrentAccountId == supplierAccountId && c.IsActive, ct);
        var delayDays = contract?.SettlementDelayDays ?? 14;

        // Varyant → ürün + grup eşlemesi (Catalog)
        var variantIds = kalemler.Select(k => k.VariantId).Distinct().ToList();
        var varyantlar = await catalogDb.ProductVariants.AsNoTracking()
            .Where(v => variantIds.Contains(v.Id))
            .Select(v => new { v.Id, v.ProductId })
            .ToListAsync(ct);
        var productByVariant = varyantlar.ToDictionary(v => v.Id, v => v.ProductId);
        var productIds = varyantlar.Select(v => v.ProductId).Distinct().ToList();
        var gruplar = await catalogDb.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.ProductGroupId })
            .ToListAsync(ct);
        var groupByProduct = gruplar.ToDictionary(g => g.Id, g => g.ProductGroupId);

        // Platform varsayılan grup oranları
        var grupIdler = gruplar.Select(g => g.ProductGroupId).Where(g => g != Guid.Empty).Distinct().ToList();
        var platformOranlari = await accountsDb.CommissionGroupRates.AsNoTracking()
            .Where(r => grupIdler.Contains(r.ProductGroupId))
            .ToDictionaryAsync(r => r.ProductGroupId, r => r.RatePercent, ct);

        // Kampanya bilgileri + katılım (opt-in şartı)
        var kampanyaIdler = kalemler.Where(k => k.CampaignId != null).Select(k => k.CampaignId!.Value).Distinct().ToList();
        var kampanyalar = kampanyaIdler.Count > 0
            ? await promotionDb.Campaigns.AsNoTracking()
                .Where(c => kampanyaIdler.Contains(c.Id))
                .Select(c => new { c.Id, c.SupplierCommissionRate, c.SupplierDiscountSharePercent, c.RequiresSupplierOptIn })
                .ToListAsync(ct)
            : [];
        var kampanyaById = kampanyalar.ToDictionary(c => c.Id);
        var katilimlar = kampanyaIdler.Count > 0
            ? await promotionDb.CampaignSupplierParticipations.AsNoTracking()
                .Where(p => kampanyaIdler.Contains(p.CampaignId) && p.SupplierAccountId == supplierAccountId && p.IsActive)
                .ToListAsync(ct)
            : [];

        // Ciro basamağı: dönem cirosu (reversed hariç mevcut hakediş satırlarının brütü).
        // Yürürlük "sonraki dönem başı" — dönem, teslim tarihine göre sözleşme tipinden hesaplanır.
        decimal? turnoverAdj = null;
        if (contract is { TurnoverTiers.Count: > 0 })
        {
            var donemBasi = DonemBasi(contract.TurnoverPeriodType, deliveredAt);
            var ciro = await accountsDb.SettlementLines.AsNoTracking()
                .Where(l => l.SupplierAccountId == supplierAccountId
                    && l.Status != "reversed" && l.ReversalOfId == null
                    && l.DeliveredAt >= donemBasi)
                .SumAsync(l => (decimal?)l.GrossAmount, ct) ?? 0m;
            turnoverAdj = contract.TurnoverTiers
                .Where(t => ciro >= t.MinTurnover)
                .OrderByDescending(t => t.MinTurnover)
                .Select(t => (decimal?)t.RateAdjustmentPercent)
                .FirstOrDefault();
        }

        foreach (var kalem in kalemler)
        {
            productByVariant.TryGetValue(kalem.VariantId, out var productId);
            Guid? groupId = productId != Guid.Empty && groupByProduct.TryGetValue(productId, out var g) && g != Guid.Empty
                ? g : null;

            decimal rate; string layer;
            decimal discountShare = 0m;

            // Kampanya bağlamı: opt-in gerekliyse katılım (+ ürün listesi) şartı aranır
            var kampanya = kalem.CampaignId is { } cid && kampanyaById.TryGetValue(cid, out var k) ? k : null;
            var kampanyaGecerli = kampanya is not null;
            if (kampanya is not null && kampanya.RequiresSupplierOptIn)
            {
                var katilim = katilimlar.FirstOrDefault(p => p.CampaignId == kampanya.Id);
                kampanyaGecerli = katilim is not null
                    && (katilim.ProductIds.Count == 0 || katilim.ProductIds.Contains(productId));
            }

            var productRate = contract?.ProductRates.FirstOrDefault(r => r.ProductId == productId);
            var contractGroupRate = groupId is { } gid1
                ? contract?.GroupRates.FirstOrDefault(r => r.ProductGroupId == gid1) : null;

            if (productRate is not null)
            {
                rate = productRate.RatePercent; layer = "product";
            }
            else if (kampanyaGecerli && kampanya!.SupplierCommissionRate is { } kOran)
            {
                rate = kOran; layer = "campaign";
            }
            else if (contractGroupRate is not null)
            {
                rate = contractGroupRate.RatePercent; layer = "contract_group";
                if (turnoverAdj is { } adj1) { rate = Math.Max(0, rate + adj1); layer += "+turnover"; }
            }
            else if (groupId is { } gid2 && platformOranlari.TryGetValue(gid2, out var varsayilan))
            {
                rate = varsayilan; layer = "group_default";
                if (turnoverAdj is { } adj2) { rate = Math.Max(0, rate + adj2); layer += "+turnover"; }
            }
            else
            {
                rate = 0m; layer = "unconfigured"; // oran tanımsız — hakediş kesintisiz yazılır, panelde görünür
            }

            if (kampanyaGecerli && kampanya!.SupplierDiscountSharePercent > 0)
                discountShare = Math.Round(kalem.Discount * kampanya.SupplierDiscountSharePercent / 100m, 2);

            var commission = Math.Round(kalem.Gross * rate / 100m, 2);
            sonuc.Add(new KalemKarari(
                kalem.OrderItemId, rate, layer, commission, discountShare,
                kampanyaGecerli ? kalem.CampaignId : null, delayDays));
        }
        return sonuc;
    }

    /// <summary>Ciro dönem başlangıcı: monthly=ay başı, yearly=yıl başı, rolling12=12 ay geriye.</summary>
    private static DateTime DonemBasi(string periodType, DateTime referans) => periodType switch
    {
        "yearly" => new DateTime(referans.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        "rolling12" => referans.AddMonths(-12),
        _ => new DateTime(referans.Year, referans.Month, 1, 0, 0, 0, DateTimeKind.Utc)
    };
}
