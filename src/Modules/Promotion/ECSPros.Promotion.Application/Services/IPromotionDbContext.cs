using ECSPros.Promotion.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Services;

public interface IPromotionDbContext
{
    DbSet<CampaignType> CampaignTypes { get; }
    DbSet<Campaign> Campaigns { get; }
    DbSet<CampaignProduct> CampaignProducts { get; }
    DbSet<Coupon> Coupons { get; }
    DbSet<CouponUsage> CouponUsages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
