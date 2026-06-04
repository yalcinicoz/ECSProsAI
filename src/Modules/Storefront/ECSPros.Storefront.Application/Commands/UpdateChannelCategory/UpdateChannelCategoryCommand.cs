using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.UpdateChannelCategory;

public record UpdateChannelCategoryCommand(
    Guid Id,
    Guid? ParentId,
    Dictionary<string, string> NameI18n,
    string Slug,
    string Status,
    string FillType,
    Dictionary<string, object>? FilterDef,
    int SortOrder,
    string? DisplayImageUrl,
    string? BadgeLabel,
    Dictionary<string, string>? MetaTitleI18n,
    Dictionary<string, string>? MetaDescriptionI18n,
    string? OgImageUrl,
    Dictionary<string, string>? OgTitleI18n) : IRequest<Result<bool>>;

public class UpdateChannelCategoryCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<UpdateChannelCategoryCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateChannelCategoryCommand request, CancellationToken ct)
    {
        var cat = await db.ChannelCategories
            .FirstOrDefaultAsync(c => c.Id == request.Id, ct);

        if (cat is null) return Result.Failure<bool>("Kanal kategorisi bulunamadı.");

        var slugConflict = await db.ChannelCategories
            .AnyAsync(c => c.FirmPlatformId == cat.FirmPlatformId
                        && c.Slug == request.Slug
                        && c.Id != request.Id, ct);
        if (slugConflict)
            return Result.Failure<bool>($"'{request.Slug}' slug'ı bu kanalda zaten kullanımda.");

        cat.ParentId           = request.ParentId;
        cat.NameI18n           = request.NameI18n;
        cat.Slug               = request.Slug;
        cat.Status             = request.Status;
        cat.FillType           = request.FillType;
        cat.FilterDef          = request.FilterDef;
        cat.SortOrder          = request.SortOrder;
        cat.DisplayImageUrl    = request.DisplayImageUrl;
        cat.BadgeLabel         = request.BadgeLabel;
        cat.MetaTitleI18n      = request.MetaTitleI18n;
        cat.MetaDescriptionI18n = request.MetaDescriptionI18n;
        cat.OgImageUrl         = request.OgImageUrl;
        cat.OgTitleI18n        = request.OgTitleI18n;
        cat.UpdatedAt          = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
