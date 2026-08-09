using ECSPros.Order.Application.Services;
using ECSPros.Order.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Infrastructure.Services;

/// <summary>OP2: core_firm_platforms'tan FirmId okur (salt-okunur raw SQL).</summary>
public class FirmResolver(OrderDbContext db) : IFirmResolver
{
    private sealed class Row { public Guid FirmId { get; set; } }

    public async Task<Guid?> GetFirmIdAsync(Guid firmPlatformId, CancellationToken ct = default)
    {
        var satirlar = await db.Database.SqlQuery<Row>($"""
            SELECT "FirmId" FROM core.core_firm_platforms
            WHERE "Id" = {firmPlatformId} AND "IsDeleted" = false
            """).ToListAsync(ct);
        return satirlar.Count == 0 ? null : satirlar[0].FirmId;
    }
}
