using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetActivePageSnapshot;

/// <summary>
/// G7: aktif yayının cache kimliği — JsonData'yı taşımadan iki kolon okunur. Anahtar
/// versiyonla birlikte snapshot Id'sini de içerir: versiyon platform içinde artan sayı
/// olduğundan tek başına anahtar olamaz (satır silinip yeniden üretilirse — test
/// ortamı, olası bakım — aynı numara farklı içeriğe denk gelir; Id global tekil).
/// Yayın yoksa Version=0.
/// </summary>
public record GetActivePageSnapshotVersionQuery(Guid FirmPlatformId) : IRequest<Result<SnapshotVersionDto>>;

public record SnapshotVersionDto(int Version, Guid SnapshotId);

public class GetActivePageSnapshotVersionQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetActivePageSnapshotVersionQuery, Result<SnapshotVersionDto>>
{
    public async Task<Result<SnapshotVersionDto>> Handle(GetActivePageSnapshotVersionQuery request, CancellationToken ct)
    {
        var aktif = await db.PublishedSnapshots
            .AsNoTracking()
            .Where(s => s.FirmPlatformId == request.FirmPlatformId && s.IsActive)
            .Select(s => new SnapshotVersionDto(s.Version, s.Id))
            .FirstOrDefaultAsync(ct);
        return Result.Success(aktif ?? new SnapshotVersionDto(0, Guid.Empty));
    }
}
