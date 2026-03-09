using ECSPros.Promotion.Application.Services;
using ECSPros.Promotion.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Commands.CreateCampaign;

public class CreateCampaignCommandHandler : IRequestHandler<CreateCampaignCommand, Result<Guid>>
{
    private readonly IPromotionDbContext _context;

    public CreateCampaignCommandHandler(IPromotionDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateCampaignCommand request, CancellationToken cancellationToken)
    {
        var exists = await _context.Campaigns.AnyAsync(c => c.Code == request.Code, cancellationToken);
        if (exists)
            return Result.Failure<Guid>($"'{request.Code}' kampanya kodu zaten mevcut.");

        var typeExists = await _context.CampaignTypes.AnyAsync(t => t.Id == request.CampaignTypeId, cancellationToken);
        if (!typeExists)
            return Result.Failure<Guid>("Kampanya tipi bulunamadı.");

        var campaign = new Campaign
        {
            CampaignTypeId = request.CampaignTypeId,
            Code = request.Code,
            NameI18n = request.NameI18n,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            Priority = request.Priority,
            ProductSelectionType = request.ProductSelectionType,
            Settings = request.Settings,
            IsActive = true
        };

        _context.Campaigns.Add(campaign);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(campaign.Id);
    }
}
