using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.UpsertChannelProductGroup;

public record UpsertChannelProductGroupCommand(
    Guid FirmPlatformId,
    Guid ProductGroupId,
    string Status) : IRequest<Result<Guid>>;

public class UpsertChannelProductGroupCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<UpsertChannelProductGroupCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(UpsertChannelProductGroupCommand request, CancellationToken ct)
    {
        var existing = await db.ChannelProductGroups
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(g => g.FirmPlatformId == request.FirmPlatformId
                                   && g.ProductGroupId == request.ProductGroupId, ct);

        if (existing is not null)
        {
            existing.Status    = request.Status;
            existing.IsDeleted = false;
            existing.DeletedAt = null;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            existing = new ChannelProductGroup
            {
                FirmPlatformId = request.FirmPlatformId,
                ProductGroupId = request.ProductGroupId,
                Status         = request.Status,
            };
            db.ChannelProductGroups.Add(existing);
        }

        await db.SaveChangesAsync(ct);
        return Result.Success(existing.Id);
    }
}
