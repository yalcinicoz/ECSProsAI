using ECSPros.Storefront.Application.Commands.SyncChannelScope;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services;

/// <summary>
/// F1 kanal kapsamı gece taraması (docs/satis-kanali-ortak-kurgu.md §3.1): filter|mixed kapsamlı her kanal
/// için SyncChannelScopeCommand. Kural: saat 03:xx (sunucu yerel) ve o gün henüz çalışmadıysa; ayrıca
/// açılıştan 3 dk sonra son sync'i 24 saati aşmış kapsamları tamamlar. Manuel tetik panelden ("Kapsamı Güncelle").
/// Legacy:Sync gibi çoklu örnek riski yok (yalnız kendi DB'sine idempotent yazar); staging'de de zararsız.
/// </summary>
public sealed class ChannelScopeSyncWorker(IServiceScopeFactory scopeFactory, ILogger<ChannelScopeSyncWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(10);
    private DateOnly? _lastNightlyRun;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken); } catch (OperationCanceledException) { return; }
        await RunAsync(onlyStale: true, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(Tick, stoppingToken); } catch (OperationCanceledException) { break; }
            var now = DateTime.Now;
            if (now.Hour == 3 && _lastNightlyRun != DateOnly.FromDateTime(now))
            {
                _lastNightlyRun = DateOnly.FromDateTime(now);
                await RunAsync(onlyStale: false, stoppingToken);
            }
        }
    }

    private async Task RunAsync(bool onlyStale, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IStorefrontDbContext>();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var threshold = DateTime.UtcNow.AddHours(-24);
            var ids = await db.ChannelScopes.AsNoTracking()
                .Where(s => (s.FillType == "filter" || s.FillType == "mixed")
                            && (!onlyStale || s.SyncedAt == null || s.SyncedAt < threshold))
                .Select(s => s.FirmPlatformId)
                .ToListAsync(ct);
            foreach (var id in ids)
            {
                var r = await mediator.Send(new SyncChannelScopeCommand(id), ct);
                if (r.IsFailure) logger.LogWarning("Kanal kapsam sync başarısız ({Channel}): {Error}", id, r.Error);
                else logger.LogInformation("Kanal kapsam sync: {Channel} → {Count} ürün", id, r.Value);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { logger.LogError(ex, "Kanal kapsam sync turu başarısız."); }
    }
}
