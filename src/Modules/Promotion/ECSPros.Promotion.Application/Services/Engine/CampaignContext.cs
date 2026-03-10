namespace ECSPros.Promotion.Application.Services.Engine;

public record CartLineItem(
    Guid VariantId,
    decimal Quantity,
    decimal UnitPrice)
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
