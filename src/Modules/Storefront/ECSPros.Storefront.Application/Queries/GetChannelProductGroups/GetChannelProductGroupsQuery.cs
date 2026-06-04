using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetChannelProductGroups;

public record GetChannelProductGroupsQuery(
    Guid FirmPlatformId,
    string? Status = null) : IRequest<Result<List<ChannelProductGroupDto>>>;

public record ChannelProductGroupDto(
    Guid Id,
    Guid FirmPlatformId,
    Guid ProductGroupId,
    string Status);

public class GetChannelProductGroupsQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetChannelProductGroupsQuery, Result<List<ChannelProductGroupDto>>>
{
    public async Task<Result<List<ChannelProductGroupDto>>> Handle(
        GetChannelProductGroupsQuery request, CancellationToken ct)
    {
        var query = db.ChannelProductGroups
            .AsNoTracking()
            .Where(g => g.FirmPlatformId == request.FirmPlatformId);

        if (request.Status is not null)
            query = query.Where(g => g.Status == request.Status);

        var items = await query
            .Select(g => new ChannelProductGroupDto(g.Id, g.FirmPlatformId, g.ProductGroupId, g.Status))
            .ToListAsync(ct);

        return Result.Success(items);
    }
}
