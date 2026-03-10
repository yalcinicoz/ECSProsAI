using ECSPros.Promotion.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Commands.UpdateCampaign;

public class UpdateCampaignCommandHandler : IRequestHandler<UpdateCampaignCommand, Result<bool>>
{
    private readonly IPromotionDbContext _context;

    public UpdateCampaignCommandHandler(IPromotionDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(UpdateCampaignCommand request, CancellationToken cancellationToken)
    {
        var campaign = await _context.Campaigns
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (campaign is null)
            return Result.Failure<bool>("Kampanya bulunamadı.");

        campaign.NameI18n = request.NameI18n;
        campaign.DescriptionI18n = request.DescriptionI18n ?? new Dictionary<string, string>();
        campaign.StartsAt = request.StartsAt;
        campaign.EndsAt = request.EndsAt;
        campaign.IsActive = request.IsActive;
        campaign.Priority = request.Priority;
        campaign.UpdatedAt = DateTime.UtcNow;
        campaign.UpdatedBy = request.UpdatedBy;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
