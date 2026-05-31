using ECSPros.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services;

/// <summary>
/// Her 30 saniyede bir dashboard metriklerini hesaplayıp DashboardHub'a gönderir.
/// </summary>
public class DashboardMetricsWorker(
    IServiceScopeFactory scopeFactory,
    IHubContext<DashboardHub> dashboardHub,
    ILogger<DashboardMetricsWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("DashboardMetricsWorker başlatıldı.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var metrics = await CollectMetricsAsync(stoppingToken);
                await dashboardHub.Clients.All.SendAsync("MetricsUpdated", metrics, stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Dashboard metrikleri toplanırken hata.");
                await Task.Delay(TimeSpan.FromSeconds(10), CancellationToken.None);
            }
        }

        logger.LogInformation("DashboardMetricsWorker durduruldu.");
    }

    private async Task<DashboardMetricsDto> CollectMetricsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();

        // Order DB
        var orderDb = scope.ServiceProvider
            .GetRequiredService<ECSPros.Order.Infrastructure.Persistence.OrderDbContext>();

        var today = DateTime.UtcNow.Date;
        var ordersToday = await orderDb.Orders
            .AsNoTracking()
            .CountAsync(o => o.CreatedAt >= today, ct);

        var revenueToday = await orderDb.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= today && o.Status != "cancelled")
            .SumAsync(o => (decimal?)o.GrandTotal ?? 0, ct);

        var pendingOrders = await orderDb.Orders
            .AsNoTracking()
            .CountAsync(o => o.Status == "pending", ct);

        var processingOrders = await orderDb.Orders
            .AsNoTracking()
            .CountAsync(o => o.Status == "processing", ct);

        // Fulfillment DB
        var fulfillmentDb = scope.ServiceProvider
            .GetRequiredService<ECSPros.Fulfillment.Infrastructure.Persistence.FulfillmentDbContext>();

        var activePlans = await fulfillmentDb.PickingPlans
            .AsNoTracking()
            .CountAsync(p => p.Status == "active", ct);

        // POS DB
        var posDb = scope.ServiceProvider
            .GetRequiredService<ECSPros.Pos.Infrastructure.Persistence.PosDbContext>();

        var posRevenueTodayRaw = await posDb.PosSales
            .AsNoTracking()
            .Where(s => s.CreatedAt >= today && s.Status == "completed")
            .SumAsync(s => (decimal?)s.GrandTotal ?? 0, ct);

        return new DashboardMetricsDto(
            ordersToday,
            revenueToday,
            pendingOrders,
            processingOrders,
            activePlans,
            posRevenueTodayRaw,
            DateTime.UtcNow);
    }
}

public record DashboardMetricsDto(
    int OrdersToday,
    decimal RevenueToday,
    int PendingOrders,
    int ProcessingOrders,
    int ActivePickingPlans,
    decimal PosRevenueToday,
    DateTime CollectedAt);
