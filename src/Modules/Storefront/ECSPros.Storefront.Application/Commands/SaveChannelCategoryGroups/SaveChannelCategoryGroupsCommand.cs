using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.SaveChannelCategoryGroups;

public record GroupInput(Guid ProductGroupId, Guid? ShowcaseProductId);

public record SaveChannelCategoryGroupsCommand(
    Guid ChannelCategoryId,
    List<GroupInput> Groups) : IRequest<Result<bool>>;

public class SaveChannelCategoryGroupsCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<SaveChannelCategoryGroupsCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(SaveChannelCategoryGroupsCommand request, CancellationToken ct)
    {
        var catExists = await db.ChannelCategories
            .AnyAsync(c => c.Id == request.ChannelCategoryId, ct);
        if (!catExists) return Result.Failure<bool>("Kanal kategorisi bulunamadı.");

        var existing = await db.ChannelCategoryGroups
            .Where(g => g.ChannelCategoryId == request.ChannelCategoryId)
            .ToListAsync(ct);

        db.ChannelCategoryGroups.RemoveRange(existing);

        foreach (var input in request.Groups.DistinctBy(g => g.ProductGroupId))
        {
            db.ChannelCategoryGroups.Add(new ChannelCategoryGroup
            {
                ChannelCategoryId  = request.ChannelCategoryId,
                ProductGroupId     = input.ProductGroupId,
                ShowcaseProductId  = input.ShowcaseProductId,
            });
        }

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
