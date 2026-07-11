using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.ReorderPageBlocks;

/// <summary>
/// G6: bir yerleşimdeki blokların sırasını verilen id dizisine göre yeniden yazar
/// (SortOrder = dizideki konum). Dizide olmayan bloklara dokunulmaz.
/// </summary>
public record ReorderPageBlocksCommand(
    Guid FirmPlatformId,
    string Placement,
    List<Guid> OrderedIds) : IRequest<Result>;

public class ReorderPageBlocksCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<ReorderPageBlocksCommand, Result>
{
    public async Task<Result> Handle(ReorderPageBlocksCommand request, CancellationToken ct)
    {
        if (request.OrderedIds.Count == 0)
            return Result.Failure("Sıra listesi boş.");

        var bloklar = await db.PageBlocks
            .Where(b => b.FirmPlatformId == request.FirmPlatformId
                     && b.Placement == request.Placement
                     && request.OrderedIds.Contains(b.Id))
            .ToListAsync(ct);

        foreach (var blok in bloklar)
            blok.SortOrder = request.OrderedIds.IndexOf(blok.Id) + 1;

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
