namespace ECSPros.Promotion.Application.Services.Engine;

public record CartLineItem(
    Guid VariantId,
    decimal Quantity,
    decimal UnitPrice,
    // 2026-08-27 (bundle): "farklı ürün" sayımı için — bilmeyen çağıranlar default bırakır,
    // o durumda bundle farklı-VARYANT sayar (belgelenmiş yedek davranış).
    Guid ProductId = default)
{
    public decimal LineTotal => Quantity * UnitPrice;
}

public record DiscountLine(
    Guid CampaignId,
    string CampaignCode,
    string CampaignName,
    string DiscountType,
    decimal DiscountAmount,
    List<Guid> AffectedVariantIds);
