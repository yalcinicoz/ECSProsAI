using ECSPros.Integration.Application.Commands.FetchMarketplaceOrders;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ECSPros.Integration.Infrastructure.Workers;

/// <summary>
/// Tüm aktif pazaryeri entegrasyonları için periyodik sipariş çekme worker'ı.
/// Her 15 dakikada bir çalışır.
/// </summary>
public class MarketplaceOrderFetchWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<MarketplaceOrderFetchWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MarketplaceOrderFetchWorker başlatıldı.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await FetchOrdersAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "MarketplaceOrderFetchWorker hata.");
            }

            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }

    private async Task FetchOrdersAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // TODO: Core modülden aktif FirmIntegration listesini çek (marketplace serviceType olanlar)
        // Şimdilik placeholder — gerçek entegrasyonda FirmIntegration tablosunu sorgula
        logger.LogDebug("Pazaryeri sipariş çekme döngüsü çalıştı.");
        await Task.CompletedTask;
    }
}
