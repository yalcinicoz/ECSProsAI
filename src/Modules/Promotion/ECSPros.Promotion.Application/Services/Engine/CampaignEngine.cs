using ECSPros.Promotion.Domain.Entities;

namespace ECSPros.Promotion.Application.Services.Engine;

public static class CampaignEngine
{
    public static DiscountLine? Calculate(
        Campaign campaign,
        IReadOnlyList<CartLineItem> cartItems,
        HashSet<Guid> applicableVariantIds)
    {
        var typeCode = campaign.CampaignType?.Code ?? string.Empty;
        var settings = campaign.Settings;

        var name = campaign.NameI18n.TryGetValue("tr", out var tr) ? tr
            : campaign.NameI18n.FirstOrDefault().Value ?? campaign.Code;

        return typeCode switch
        {
            "percentage_discount" => ApplyPercentageDiscount(campaign, name, cartItems, applicableVariantIds, settings),
            "fixed_discount"      => ApplyFixedDiscount(campaign, name, cartItems, applicableVariantIds, settings),
            "buy_x_get_y"         => ApplyBuyXGetY(campaign, name, cartItems, applicableVariantIds, settings),
            "min_cart_discount"   => ApplyMinCartDiscount(campaign, name, cartItems, settings),
            _                     => null
        };
    }

    // ─── percentage_discount ─────────────────────────────────────────
    // Settings: { "discountRate": 20.0, "maxDiscountAmount": 100.0? }
    private static DiscountLine? ApplyPercentageDiscount(
        Campaign campaign, string name,
        IReadOnlyList<CartLineItem> cartItems,
        HashSet<Guid> applicableVariantIds,
        Dictionary<string, object> settings)
    {
        var rate = GetDecimal(settings, "discountRate");
        if (rate <= 0) return null;

        var affectedItems = applicableVariantIds.Count == 0
            ? cartItems
            : cartItems.Where(i => applicableVariantIds.Contains(i.VariantId)).ToList();

        var subtotal = affectedItems.Sum(i => i.LineTotal);
        var discount = Math.Round(subtotal * rate / 100, 2);

        if (settings.TryGetValue("maxDiscountAmount", out var maxObj))
            discount = Math.Min(discount, GetDecimal(settings, "maxDiscountAmount"));

        if (discount <= 0) return null;

        return new DiscountLine(campaign.Id, campaign.Code, name, "percentage_discount",
            discount, affectedItems.Select(i => i.VariantId).ToList());
    }

    // ─── fixed_discount ──────────────────────────────────────────────
    // Settings: { "discountAmount": 50.0, "minCartTotal": 200.0? }
    private static DiscountLine? ApplyFixedDiscount(
        Campaign campaign, string name,
        IReadOnlyList<CartLineItem> cartItems,
        HashSet<Guid> applicableVariantIds,
        Dictionary<string, object> settings)
    {
        var amount = GetDecimal(settings, "discountAmount");
        if (amount <= 0) return null;

        var affectedItems = applicableVariantIds.Count == 0
            ? cartItems
            : cartItems.Where(i => applicableVariantIds.Contains(i.VariantId)).ToList();

        var subtotal = affectedItems.Sum(i => i.LineTotal);

        if (settings.TryGetValue("minCartTotal", out _))
        {
            var min = GetDecimal(settings, "minCartTotal");
            if (subtotal < min) return null;
        }

        var discount = Math.Min(amount, subtotal); // indirim toplamı geçemez

        return new DiscountLine(campaign.Id, campaign.Code, name, "fixed_discount",
            discount, affectedItems.Select(i => i.VariantId).ToList());
    }

    // ─── buy_x_get_y ─────────────────────────────────────────────────
    // Settings: { "buyQuantity": X, "getQuantity": Y, "sameProduct": bool (vars. true),
    //             "cheapestGetsBenefit": bool (vars. true),
    //             "getBenefitType": "free"|"percent"|"amount", "getBenefitValue": n }
    // sameProduct=true  → her kalem kendi içinde X+Y'lik setler kurar (eski davranış).
    // sameProduct=false → kapsamdaki TÜM birimler tek havuzda toplanır; farklı ürünler
    //                     birlikte set oluşturur (örn. 2 farklı üründen 1'er adet = 1 set).
    // Y birimleri cheapestGetsBenefit'e göre en ucuzdan (true) ya da en pahalıdan (false)
    // seçilir; free=%100, percent=birim fiyatın yüzdesi, amount=birim başına sabit tutar
    // (birim fiyatı aşamaz).
    private static DiscountLine? ApplyBuyXGetY(
        Campaign campaign, string name,
        IReadOnlyList<CartLineItem> cartItems,
        HashSet<Guid> applicableVariantIds,
        Dictionary<string, object> settings)
    {
        var buyQty = (int)GetDecimal(settings, "buyQuantity");
        var getQty = (int)GetDecimal(settings, "getQuantity");
        if (buyQty <= 0 || getQty <= 0) return null;

        var sameProduct = GetBool(settings, "sameProduct", defaultValue: true);
        var cheapestFirst = GetBool(settings, "cheapestGetsBenefit", defaultValue: true);
        var benefitType = GetString(settings, "getBenefitType") ?? "free";
        var benefitValue = GetDecimal(settings, "getBenefitValue");
        if (benefitType is "percent" or "amount" && benefitValue <= 0) return null;

        var affectedItems = applicableVariantIds.Count == 0
            ? cartItems.ToList()
            : cartItems.Where(i => applicableVariantIds.Contains(i.VariantId)).ToList();
        if (affectedItems.Count == 0) return null;

        // Havuzlar: sameProduct=true'da kalem başına, false'ta tek ortak havuz
        IEnumerable<List<CartLineItem>> pools = sameProduct
            ? affectedItems.Select(i => new List<CartLineItem> { i })
            : new[] { affectedItems };

        decimal totalDiscount = 0;
        var affectedVariants = new List<Guid>();

        foreach (var pool in pools)
        {
            // Birim listesi (adet kadar birim fiyat) — Y seçim sırası fiyat esaslı
            var units = pool
                .SelectMany(i => Enumerable.Repeat((i.VariantId, i.UnitPrice), (int)i.Quantity))
                .ToList();
            var sets = units.Count / (buyQty + getQty);
            if (sets <= 0) continue;

            var benefitUnits = (cheapestFirst
                    ? units.OrderBy(u => u.UnitPrice)
                    : units.OrderByDescending(u => u.UnitPrice))
                .Take(sets * getQty)
                .ToList();

            foreach (var (variantId, unitPrice) in benefitUnits)
            {
                var indirim = benefitType switch
                {
                    "percent" => Math.Round(unitPrice * benefitValue / 100, 2),
                    "amount" => Math.Min(benefitValue, unitPrice),
                    _ => unitPrice // free
                };
                if (indirim <= 0) continue;
                totalDiscount += indirim;
                if (!affectedVariants.Contains(variantId)) affectedVariants.Add(variantId);
            }
        }

        if (totalDiscount <= 0) return null;

        return new DiscountLine(campaign.Id, campaign.Code, name, "buy_x_get_y",
            Math.Round(totalDiscount, 2), affectedVariants);
    }

    // ─── min_cart_discount ───────────────────────────────────────────
    // Settings: { "minCartTotal": 500.0, "discountRate": 10.0 }
    private static DiscountLine? ApplyMinCartDiscount(
        Campaign campaign, string name,
        IReadOnlyList<CartLineItem> cartItems,
        Dictionary<string, object> settings)
    {
        var minTotal = GetDecimal(settings, "minCartTotal");
        var rate = GetDecimal(settings, "discountRate");
        if (minTotal <= 0 || rate <= 0) return null;

        var cartTotal = cartItems.Sum(i => i.LineTotal);
        if (cartTotal < minTotal) return null;

        var discount = Math.Round(cartTotal * rate / 100, 2);

        return new DiscountLine(campaign.Id, campaign.Code, name, "min_cart_discount",
            discount, cartItems.Select(i => i.VariantId).ToList());
    }

    // ─── Yardımcı ────────────────────────────────────────────────────
    private static bool GetBool(Dictionary<string, object> settings, string key, bool defaultValue)
    {
        if (!settings.TryGetValue(key, out var val) || val is null) return defaultValue;
        return val switch
        {
            bool b => b,
            string s => bool.TryParse(s, out var p) ? p : defaultValue,
            System.Text.Json.JsonElement je => je.ValueKind switch
            {
                System.Text.Json.JsonValueKind.True => true,
                System.Text.Json.JsonValueKind.False => false,
                _ => defaultValue
            },
            _ => defaultValue
        };
    }

    private static string? GetString(Dictionary<string, object> settings, string key)
    {
        if (!settings.TryGetValue(key, out var val) || val is null) return null;
        return val switch
        {
            string s => s,
            System.Text.Json.JsonElement je => je.ValueKind == System.Text.Json.JsonValueKind.String
                            ? je.GetString() : je.ToString(),
            _ => val.ToString()
        };
    }

    private static decimal GetDecimal(Dictionary<string, object> settings, string key)
    {
        if (!settings.TryGetValue(key, out var val)) return 0;
        return val switch
        {
            decimal d  => d,
            double db  => (decimal)db,
            long l     => (decimal)l,
            int i      => (decimal)i,
            string s   => decimal.TryParse(s, out var p) ? p : 0,
            System.Text.Json.JsonElement je => je.ValueKind == System.Text.Json.JsonValueKind.Number
                            ? je.GetDecimal() : 0,
            _ => 0
        };
    }
}
