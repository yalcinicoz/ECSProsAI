using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Promotion.Application.Commands.CreateCampaign;

// F1: platform bir kampanya tipini uygular. Settings tip şablonuna göre doldurulur; ürün kapsamı
// kategoriyle aynı (FillType + FilterDef + manuel/dışlama listeleri).
public record CreateCampaignCommand(
    Guid FirmPlatformId,
    Guid CampaignTypeId,
    string Code,
    Dictionary<string, string> NameI18n,
    Dictionary<string, string>? DescriptionI18n,
    string? BadgeLabel,
    DateTime StartsAt,
    DateTime? EndsAt,
    int Priority,
    bool IsActive,
    Dictionary<string, object> Settings,
    string FillType,
    Dictionary<string, object>? FilterDef,
    List<Guid>? ManualProductIds,
    List<Guid>? ExcludedProductIds,
    Guid? CreatedBy = null) : IRequest<Result<Guid>>;
