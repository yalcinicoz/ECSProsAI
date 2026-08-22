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
    string ListingMode,
    Dictionary<string, object>? FilterDef,
    int SortOrder,
    string? DisplayImageUrl,
    string? BadgeLabel,
    Dictionary<string, string>? MetaTitleI18n,
    Dictionary<string, string>? MetaDescriptionI18n,
    string? OgImageUrl,
    Dictionary<string, string>? OgTitleI18n,
    string? GoogleCategoryId = null) : IRequest<Result<bool>>;

public class UpdateChannelCategoryCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<UpdateChannelCategoryCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateChannelCategoryCommand request, CancellationToken ct)
    {
        var cat = await db.ChannelCategories
            .FirstOrDefaultAsync(c => c.Id == request.Id, ct);

        if (cat is null) return Result.Failure<bool>("Kanal kategorisi bulunamadı.");

        // Yeni/değişen slug normalize edilir — nokta/virgül gibi karakterler yeni URL'lerde
        // yasak (rota yalnız eski sistemden taşınan ürün URL'leri için tolere eder).
        var slug = Helpers.UrlSlug.Normalize(request.Slug);
        if (slug.Length == 0)
            return Result.Failure<bool>("Geçerli bir URL üretilemedi — slug harf/rakam içermeli.");

        var slugConflict = await db.ChannelCategories
            .AnyAsync(c => c.FirmPlatformId == cat.FirmPlatformId
                        && c.Slug == slug
                        && c.Id != request.Id, ct);
        if (slugConflict)
            return Result.Failure<bool>($"'{slug}' URL'i bu kanalda zaten kullanımda.");

        cat.ParentId           = request.ParentId;
        cat.NameI18n           = request.NameI18n;
        cat.Slug               = slug;
        cat.Status             = request.Status;
        cat.FillType           = request.FillType;
        cat.ListingMode        = request.ListingMode;
        cat.FilterDef          = request.FilterDef;
        cat.SortOrder          = request.SortOrder;
        cat.DisplayImageUrl    = request.DisplayImageUrl;
        cat.BadgeLabel         = request.BadgeLabel;
        cat.MetaTitleI18n      = request.MetaTitleI18n;
        cat.MetaDescriptionI18n = request.MetaDescriptionI18n;
        cat.OgImageUrl         = request.OgImageUrl;
        cat.OgTitleI18n        = request.OgTitleI18n;
        cat.GoogleCategoryId   = string.IsNullOrWhiteSpace(request.GoogleCategoryId) ? null : request.GoogleCategoryId.Trim();
        cat.UpdatedAt          = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
