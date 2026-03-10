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
    // Settings: { "buyQuantity": 2, "getQuantity": 1 }
    // Her ürün kendi içinde değerlendiriliyor (ucuzdan pahalıya ücretsiz)
    private static DiscountLine? ApplyBuyXGetY(
        Campaign campaign, string name,
        IReadOnlyList<CartLineItem> cartItems,
        HashSet<Guid> applicableVariantIds,
        Dictionary<string, object> settings)
    {
        var buyQty = (int)GetDecimal(settings, "buyQuantity");
        var getQty = (int)GetDecimal(settings, "getQuantity");
        if (buyQty <= 0 || getQty <= 0) return null;

        var affectedItems = applicableVariantIds.Count == 0
            ? cartItems.ToList()
            : cartItems.Where(i => applicableVariantIds.Contains(i.VariantId)).ToList();

        decimal totalDiscount = 0;
        var affectedVariants = new List<Guid>();

        foreach (var item in affectedItems)
        {
            var totalQty = (int)item.Quantity;
            var sets = totalQty / (buyQty + getQty);
            if (sets <= 0) continue;

            var freeQty = sets * getQty;
            totalDiscount += Math.Round(freeQty * item.UnitPrice, 2);
            affectedVariants.Add(item.VariantId);
        }

        if (totalDiscount <= 0) return null;

        return new DiscountLine(campaign.Id, campaign.Code, name, "buy_x_get_y",
            totalDiscount, affectedVariants);
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
