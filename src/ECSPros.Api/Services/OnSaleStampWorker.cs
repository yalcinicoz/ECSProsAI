using ECSPros.Shared.Contracts.Channels;
using Npgsql;

namespace ECSPros.Api.Services;

/// <summary>
/// T6 (İ5/K6): "satışa giriş" damgası — yerleştirilmiş (placed) sayım kayıtlarının ürünü herhangi bir
/// SİTE kanalında (pushListing=false, orderDirection=internal) F2 `published` olduğunda OnSaleAt yazılır.
/// Gün hassasiyeti yeterlidir: açılıştan 5 dk sonra + 6 saatte bir tur. Damga geri alınmaz (ilk satışa giriş anı).
/// </summary>
public sealed class OnSaleStampWorker(
    IServiceScopeFactory scopeFactory,
    NpgsqlDataSource dataSource,
    ILogger<OnSaleStampWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); } catch (OperationCanceledException) { return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "OnSale damga turu başarısız."); }
            try { await Task.Delay(Tick, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var caps = scope.ServiceProvider.GetRequiredService<IChannelCapabilityResolver>();
        var listing = scope.ServiceProvider.GetRequiredService<ChannelListingStatusService>();

        // Damga bekleyen kayıt yoksa hiç hesaplama yapma
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        long bekleyen;
        await using (var c0 = new NpgsqlCommand(
            @"SELECT COUNT(*) FROM procurement.sorting_entries
              WHERE ""PutawayStatus""='placed' AND ""OnSaleAt"" IS NULL AND NOT ""IsDeleted""", conn))
            bekleyen = (long)(await c0.ExecuteScalarAsync(ct))!;
        if (bekleyen == 0) return;

        // Site kanalları (yetenek: push yok, sipariş içeride)
        var channels = new List<Guid>();
        await using (var c1 = new NpgsqlCommand(
            @"SELECT fp.""Id"" FROM core.core_firm_platforms fp WHERE fp.""IsActive"" AND NOT fp.""IsDeleted""", conn))
        await using (var r = await c1.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) channels.Add(r.GetGuid(0));

        var published = new HashSet<Guid>();
        foreach (var chId in channels)
        {
            var cap = await caps.GetAsync(chId, ct);
            if (cap.PushListing || cap.OrderDirection != ChannelCapabilities.OrderDirections.Internal) continue;
            foreach (var pid in await listing.GetProductIdsByStatusAsync(chId, "published", null, ct))
                published.Add(pid);
        }
        if (published.Count == 0) return;

        const string stampSql = @"
            UPDATE procurement.sorting_entries e SET ""OnSaleAt"" = now()
            FROM catalog.product_variants v
            WHERE v.""Id"" = e.""VariantId"" AND e.""PutawayStatus"" = 'placed'
              AND e.""OnSaleAt"" IS NULL AND NOT e.""IsDeleted""
              AND v.""ProductId"" = ANY(@ids)";
        await using var c2 = new NpgsqlCommand(stampSql, conn) { CommandTimeout = 60 };
        c2.Parameters.AddWithValue("ids", published.ToArray());
        var n = await c2.ExecuteNonQueryAsync(ct);
        if (n > 0) logger.LogInformation("OnSale damgası: {N} sayım kaydı satışa girdi.", n);
    }
}
