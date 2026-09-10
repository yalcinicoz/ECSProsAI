using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetChannelCategoryBySlug;

/// <summary>Yayınlı bir kanal kategorisini slug'ından çözer (2026-07-30). Kategori sayfası
/// routing'i nav ağacında bulamadığında (menüye bağlı olmayan kategori) doğrudan URL erişimi
/// için kullanılır — yayınlı her kategori URL'iyle açılabilmeli.</summary>
public record GetChannelCategoryBySlugQuery(Guid FirmPlatformId, string Slug)
    : IRequest<Result<ChannelCategorySlugDto>>;

// SEO alanları (2026-09-10, panel SEO sekmesiyle birlikte): kategori sayfası <title>/meta
// description/og:* değerlerini buradan alır; boşsa sayfa kategori adı + site varsayılanına düşer.
public record ChannelCategorySlugDto(
    Guid Id,
    Dictionary<string, string> NameI18n,
    string Slug,
    string? DisplayImageUrl,
    string? BadgeLabel,
    Dictionary<string, string>? MetaTitleI18n = null,
    Dictionary<string, string>? MetaDescriptionI18n = null,
    string? OgImageUrl = null,
    Dictionary<string, string>? OgTitleI18n = null);

public class GetChannelCategoryBySlugQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetChannelCategoryBySlugQuery, Result<ChannelCategorySlugDto>>
{
    public async Task<Result<ChannelCategorySlugDto>> Handle(
        GetChannelCategoryBySlugQuery request, CancellationToken ct)
    {
        var kat = await db.ChannelCategories
            .AsNoTracking()
            .Where(c => c.FirmPlatformId == request.FirmPlatformId
                && c.Slug == request.Slug
                && c.Status == "published")
            .Select(c => new ChannelCategorySlugDto(
                c.Id, c.NameI18n, c.Slug, c.DisplayImageUrl, c.BadgeLabel,
                c.MetaTitleI18n, c.MetaDescriptionI18n, c.OgImageUrl, c.OgTitleI18n))
            .FirstOrDefaultAsync(ct);

        return kat is null
            ? Result.Failure<ChannelCategorySlugDto>("Kategori bulunamadı.")
            : Result.Success(kat);
    }
}
