using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Promotion.Application.Queries.GetCampaigns;

public record GetCampaignsQuery(bool ActiveOnly = true) : IRequest<Result<List<CampaignDto>>>;

public record CampaignDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    DateTime StartsAt,
    DateTime? EndsAt,
    bool IsActive,
    int Priority,
    string ProductSelectionType);
