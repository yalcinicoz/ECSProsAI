namespace ECSPros.Api.Services.Push;

/// <summary>
/// Push worker: 15 sn'de bir kuyruğu işler (DistributedWorkerLock "push-dispatch"), Push:ScanMinutes (15) aralıkla
/// zamanlanmış senaryoları tarar ("push-scan"). Push:Enabled=false → uyur. FCM servis hesabı yoksa kuyruk birikir, hata yok.
/// </summary>
public sealed class PushGondericiWorker(IServiceScopeFactory scopeFactory, DistributedWorkerLock workerLock, IConfiguration config, ILogger<PushGondericiWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken st)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), st);
        var sonTarama = DateTime.MinValue;
        while (!st.IsCancellationRequested)
        {
            try
            {
                if (config.GetValue("Push:Enabled", true))
                {
                    await using (var lease = await workerLock.TryAcquireAsync("push-dispatch", st))
                        if (lease is not null)
                        {
                            using var scope = scopeFactory.CreateScope();
                            var (sent, failed, skipped) = await scope.ServiceProvider.GetRequiredService<PushGonderimServisi>().IsleAsync(200, st);
                            if (sent + failed + skipped > 0) logger.LogInformation("Push: gönderildi {Sent}, hata {Failed}, atlandı {Skipped}", sent, failed, skipped);
                        }
                    var aralik = TimeSpan.FromMinutes(Math.Max(1, config.GetValue("Push:ScanMinutes", 15)));
                    if (DateTime.UtcNow - sonTarama >= aralik)
                    {
                        await using var lease = await workerLock.TryAcquireAsync("push-scan", st);
                        if (lease is not null)
                        {
                            sonTarama = DateTime.UtcNow;
                            using var scope = scopeFactory.CreateScope();
                            await scope.ServiceProvider.GetRequiredService<PushTarayici>().TaraAsync(st);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (st.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Push worker turu hata"); }
            await Task.Delay(TimeSpan.FromSeconds(15), st);
        }
    }
}
