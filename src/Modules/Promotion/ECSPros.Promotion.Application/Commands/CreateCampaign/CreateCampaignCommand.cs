using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Promotion.Application.Commands.CreateCampaign;

public record CreateCampaignCommand(
    Guid CampaignTypeId,
    string Code,
    Dictionary<string, string> NameI18n,
    DateTime StartsAt,
    DateTime? EndsAt,
    int Priority,
    string ProductSelectionType,
    Dictionary<string, object> Settings) : IRequest<Result<Guid>>;
