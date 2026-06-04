using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.SaveChannelCategoryGroups;

public record SaveChannelCategoryGroupsCommand(
    Guid ChannelCategoryId,
    List<Guid> ProductGroupIds) : IRequest<Result<bool>>;

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

        foreach (var groupId in request.ProductGroupIds.Distinct())
        {
            db.ChannelCategoryGroups.Add(new ChannelCategoryGroup
            {
                ChannelCategoryId = request.ChannelCategoryId,
                ProductGroupId    = groupId,
            });
        }

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
