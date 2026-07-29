using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.CreateChannelCategory;

public record CreateChannelCategoryCommand(
    Guid FirmPlatformId,
    Guid? ParentId,
    Dictionary<string, string> NameI18n,
    string? Slug,
    string FillType,
    Dictionary<string, object>? FilterDef,
    int SortOrder,
    string? DisplayImageUrl,
    string? BadgeLabel) : IRequest<Result<Guid>>;

public class CreateChannelCategoryCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<CreateChannelCategoryCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateChannelCategoryCommand request, CancellationToken ct)
    {
        // Elle girilen slug da normalize edilir — nokta/virgül gibi karakterler yeni
        // URL'lerde yasak (rota yalnız eski sistemden taşınanlar için tolere eder).
        var slug = Helpers.UrlSlug.Normalize(string.IsNullOrWhiteSpace(request.Slug)
            ? request.NameI18n.GetValueOrDefault("tr") ?? request.NameI18n.Values.FirstOrDefault() ?? "kategori"
            : request.Slug);
        if (slug.Length == 0)
            return Result.Failure<Guid>("Geçerli bir URL üretilemedi — ad veya slug harf/rakam içermeli.");

        var slugExists = await db.ChannelCategories
            .AnyAsync(c => c.FirmPlatformId == request.FirmPlatformId && c.Slug == slug, ct);
        if (slugExists)
            return Result.Failure<Guid>($"'{slug}' URL'i bu kanalda zaten kullanımda.");

        var cat = new ChannelCategory
        {
            FirmPlatformId   = request.FirmPlatformId,
            ParentId         = request.ParentId,
            NameI18n         = request.NameI18n,
            Slug             = slug,
            Status           = "draft",
            FillType         = request.FillType,
            FilterDef        = request.FilterDef,
            SortOrder        = request.SortOrder,
            DisplayImageUrl  = request.DisplayImageUrl,
            BadgeLabel       = request.BadgeLabel,
        };

        db.ChannelCategories.Add(cat);
        await db.SaveChangesAsync(ct);
        return Result.Success(cat.Id);
    }

}
