using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Promotion.Application.Commands.UpdateCampaign;

// F1: kampanya güncelleme — genel alanlar + Settings (şablona göre) + ürün kapsamı (FillType/
// FilterDef/manuel/dışlama). Ürün kapsamı her kayıtta yeniden materyalize edilir.
public record UpdateCampaignCommand(
    Guid Id,
    Dictionary<string, string> NameI18n,
    Dictionary<string, string>? DescriptionI18n,
    string? BadgeLabel,
    DateTime StartsAt,
    DateTime? EndsAt,
    bool IsActive,
    int Priority,
    Dictionary<string, object> Settings,
    string FillType,
    Dictionary<string, object>? FilterDef,
    List<Guid>? ManualProductIds,
    List<Guid>? ExcludedProductIds,
    Guid UpdatedBy) : IRequest<Result<bool>>;
