using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Domain.Entities;
using ECSPros.Integration.Application.Adapters;
using ECSPros.Integration.Application.Commands.CreateCargoShipment;
using ECSPros.Order.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.Fulfillment;

/// <summary>
/// OP5 (K-10): kargo bildirim worker'ı — outbox'taki pending kayıtları taşıyıcı API'sine
/// gönderir (legacy senkron outbox kalıbı). VARSAYILAN KAPALI (CargoNotify:Enabled=false):
/// adapter'lar stub olduğundan gerçek taşıyıcılar bağlanana (KG1) dek kuyruk birikir;
/// açılınca birikenler işlenir. Hata: üstel geri çekilme, 10 denemede failed.
/// </summary>
public class CargoNotifyWorker(IServiceScopeFactory scopeFactory,
    IConfiguration config, DistributedWorkerLock workerLock,
    ILogger<CargoNotifyWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken st)
    {
        logger.LogInformation("CargoNotifyWorker: {Durum}",
            config.GetValue("CargoNotify:Enabled", false) ? "AKTİF" : "KAPALI (kuyruk birikir — KG1'de açılacak)");
        while (!st.IsCancellationRequested)
        {
            try
            {
                if (config.GetValue("CargoNotify:Enabled", false))
                    await KuyruguIsleAsync(st);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "CargoNotifyWorker döngü hatası");
            }
            await Task.Delay(TimeSpan.FromSeconds(60), st);
        }
    }

    private async Task KuyruguIsleAsync(CancellationToken ct)
    {
        await using var lease = await workerLock.TryAcquireAsync("cargo-notify", ct);
        if (lease is null) return;

        using var scope = scopeFactory.CreateScope();
        var fulDb = scope.ServiceProvider.GetRequiredService<IFulfillmentDbContext>();
        var orderDb = scope.ServiceProvider.GetRequiredService<IOrderDbContext>();
        var orderReader = scope.ServiceProvider.GetRequiredService<IOrderPackagingReader>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var now = DateTime.UtcNow;
        var bekleyenler = await fulDb.CargoNotifyOutbox
            .Where(o => o.Status == "pending" && o.CargoIntegrationId != null
                        && (o.NextAttemptAt == null || o.NextAttemptAt <= now))
            .OrderBy(o => o.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        foreach (var kayit in bekleyenler)
        {
            try
            {
                // Taşıyıcı servis kodu (core entegrasyon kaydı → definition servis kataloğu)
                var kodlar = await ((DbContext)fulDb).Database.SqlQuery<ServiceCodeRow>($"""
                    SELECT s."Code" FROM core.core_firm_platform_integrations i
                    JOIN definition.integration_services s ON s."Id" = i."IntegrationServiceId"
                    WHERE i."Id" = {kayit.CargoIntegrationId} AND i."IsDeleted" = false
                    """).ToListAsync(ct);
                if (kodlar.Count == 0)
                    throw new InvalidOperationException("Taşıyıcı servis kodu çözülemedi.");

                var siparis = await orderReader.GetOrderAsync(kayit.OrderId, ct)
                    ?? throw new InvalidOperationException("Sipariş okunamadı.");
                var istek = new CargoShipmentRequest(
                    kayit.OrderId,
                    siparis.RecipientName ?? "", siparis.RecipientPhone ?? "",
                    siparis.AddressLine ?? "", "", "", 1, null, null);

                var sonuc = await mediator.Send(new CreateCargoShipmentCommand(
                    kayit.CargoIntegrationId!.Value, kodlar[0].Code, istek), ct);
                if (sonuc.IsFailure)
                    throw new InvalidOperationException(sonuc.Error);

                if (kayit.ShipmentId is { } sid)
                {
                    var shipment = await orderDb.Shipments.FirstOrDefaultAsync(s => s.Id == sid, ct);
                    if (shipment is not null)
                    {
                        shipment.TrackingNumber = sonuc.Value!.TrackingNumber;
                        shipment.TrackingUrl = sonuc.Value.TrackingUrl;
                        shipment.ApiStatus = "sent";
                        shipment.ApiSentAt = DateTime.UtcNow;
                    }
                    await orderDb.SaveChangesAsync(ct);
                }
                kayit.Status = "sent";
                kayit.SentAt = DateTime.UtcNow;
                kayit.LastError = null;
                fulDb.OperationLogs.Add(new OperationLog
                {
                    OrderId = kayit.OrderId, PackageId = kayit.PackageId, Action = "cargo_notified",
                    ActorId = kayit.CreatedBy ?? Guid.Empty, CreatedBy = kayit.CreatedBy,
                    Detail = new Dictionary<string, object>
                        { ["cargo"] = kayit.CargoName ?? "", ["tracking"] = sonuc.Value!.TrackingNumber ?? "" }
                });
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                kayit.AttemptCount++;
                kayit.LastError = e.Message.Length > 1900 ? e.Message[..1900] : e.Message;
                if (kayit.AttemptCount >= 10) kayit.Status = "failed";
                else kayit.NextAttemptAt = DateTime.UtcNow.AddMinutes(Math.Min(Math.Pow(2, kayit.AttemptCount), 60));
                logger.LogWarning("Kargo bildirimi başarısız (paket {Paket}, deneme {N}): {Hata}",
                    kayit.PackageId, kayit.AttemptCount, e.Message);
            }
        }
        if (bekleyenler.Count > 0)
            await fulDb.SaveChangesAsync(ct);
    }

    private sealed class ServiceCodeRow { public string Code { get; set; } = string.Empty; }
}
