using ECSPros.Promotion.Application.Services;
using ECSPros.Promotion.Application.Services.Engine;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Queries.CalculateDiscounts;

public class CalculateDiscountsQueryHandler : IRequestHandler<CalculateDiscountsQuery, Result<List<DiscountLine>>>
{
    private readonly IPromotionDbContext _context;

    public CalculateDiscountsQueryHandler(IPromotionDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<DiscountLine>>> Handle(CalculateDiscountsQuery request, CancellationToken cancellationToken)
    {
        if (!request.Items.Any())
            return Result.Success(new List<DiscountLine>());

        var now = DateTime.UtcNow;

        // Aktif kampanyaları öncelik sırasına göre çek
        var campaigns = await _context.Campaigns
            .Include(c => c.CampaignType)
            .Include(c => c.Products)
            .Where(c => c.IsActive
                     && c.StartsAt <= now
                     && (c.EndsAt == null || c.EndsAt >= now))
            .OrderByDescending(c => c.Priority)
            .ToListAsync(cancellationToken);

        var cartItems = request.Items.AsReadOnly();
        var results = new List<DiscountLine>();
        var stackableCampaigns = new List<DiscountLine>();
        bool nonStackableApplied = false;

        foreach (var campaign in campaigns)
        {
            // Hangi variant'lar bu kampanya kapsamında?
            var applicableVariantIds = GetApplicableVariants(campaign, request.Items);

            var discountLine = CampaignEngine.Calculate(campaign, cartItems, applicableVariantIds);
            if (discountLine is null) continue;

            var isStackable = campaign.CampaignType?.IsStackable ?? false;

            if (isStackable)
            {
                stackableCampaigns.Add(discountLine);
            }
            else
            {
                // Non-stackable: sadece ilk (en yüksek öncelikli) uygulanır
                if (!nonStackableApplied)
                {
                    results.Add(discountLine);
                    nonStackableApplied = true;
                }
            }
        }

        // Stackable'ları ekle
        results.AddRange(stackableCampaigns);

        return Result.Success(results);
    }

    private static HashSet<Guid> GetApplicableVariants(
        ECSPros.Promotion.Domain.Entities.Campaign campaign,
        List<CartLineItem> cartItems)
    {
        return campaign.ProductSelectionType switch
        {
            "all" => new HashSet<Guid>(), // boş = hepsi
            "specific" => campaign.Products
                .Where(p => p.VariantId.HasValue
                         && cartItems.Any(ci => ci.VariantId == p.VariantId.Value))
                .Select(p => p.VariantId!.Value)
                .ToHashSet(),
            _ => new HashSet<Guid>()
        };
    }
}
