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
    string Slug,
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
        var slugExists = await db.ChannelCategories
            .AnyAsync(c => c.FirmPlatformId == request.FirmPlatformId && c.Slug == request.Slug, ct);
        if (slugExists)
            return Result.Failure<Guid>($"'{request.Slug}' slug'ı bu kanalda zaten kullanımda.");

        var cat = new ChannelCategory
        {
            FirmPlatformId   = request.FirmPlatformId,
            ParentId         = request.ParentId,
            NameI18n         = request.NameI18n,
            Slug             = request.Slug,
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
