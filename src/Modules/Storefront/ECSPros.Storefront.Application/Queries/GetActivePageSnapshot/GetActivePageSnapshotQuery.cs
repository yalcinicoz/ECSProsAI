using System.Text.Json;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Commands.PublishPageSnapshot;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetActivePageSnapshot;

/// <summary>
/// G4: canlı okuma — platformun aktif snapshot'ını çözülmüş modele deserialize eder.
/// Yayın yoksa null döner (hata değil: vitrin henüz kurulmamış platformda sayfa
/// bloksuz render edilir). Taslak tablolara BAKILMAZ.
/// </summary>
public record GetActivePageSnapshotQuery(Guid FirmPlatformId) : IRequest<Result<PageSnapshotDto?>>;

public class GetActivePageSnapshotQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetActivePageSnapshotQuery, Result<PageSnapshotDto?>>
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<Result<PageSnapshotDto?>> Handle(GetActivePageSnapshotQuery request, CancellationToken ct)
    {
        var json = await db.PublishedSnapshots
            .AsNoTracking()
            .Where(s => s.FirmPlatformId == request.FirmPlatformId && s.IsActive)
            .Select(s => s.JsonData)
            .FirstOrDefaultAsync(ct);
        if (json is null)
            return Result.Success<PageSnapshotDto?>(null);

        try
        {
            return Result.Success(JsonSerializer.Deserialize<PageSnapshotDto>(json, JsonOpts));
        }
        catch (JsonException)
        {
            // Bozuk snapshot sayfayı düşürmez — bloksuz render (rollback ile kurtarılır)
            return Result.Success<PageSnapshotDto?>(null);
        }
    }
}
