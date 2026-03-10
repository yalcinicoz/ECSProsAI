using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Promotion.Application.Commands.UpdateCampaign;

public record UpdateCampaignCommand(
    Guid Id,
    Dictionary<string, string> NameI18n,
    Dictionary<string, string>? DescriptionI18n,
    DateTime StartsAt,
    DateTime? EndsAt,
    bool IsActive,
    int Priority,
    Guid UpdatedBy) : IRequest<Result<bool>>;
