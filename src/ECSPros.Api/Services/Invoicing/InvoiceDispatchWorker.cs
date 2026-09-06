using ECSPros.Integration.Application.Adapters;
using ECSPros.Integration.Application.Services;
using ECSPros.Integration.Domain.Entities;
using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.Invoicing;

/// <summary>
/// FE4 (docs/fatura-entegrasyon-plani.md §2.6): fatura gönderim outbox worker'ı. ord_invoice_dispatches'taki
/// bekleyen send/cancel işlerini seriye bağlı entegratör sözleşmesi üzerinden IEInvoiceAdapter'a verir.
/// VARSAYILAN KAPALI (InvoiceDispatch:Enabled=false): gerçek adaptör (K1) gelene dek kuyruk birikir.
/// Güvenlik: taslak (IsStub) adaptör yalnız testMode=true sözleşmede çalışır; aksi halde iş "blocked".
/// Hata: üstel geri çekilme (1/5/30/120/360 dk), MaxAttempts sonrası "dead" (panelden Tekrar Dene).
/// Yalnız Node:Role=Worker|Both (GenelWorkerRolu) düğümde kayıtlıdır; DistributedWorkerLock ile tek koşucu.
/// </summary>
public class InvoiceDispatchWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    DistributedWorkerLock workerLock,
    ILogger<InvoiceDispatchWorker> logger) : BackgroundService
{
    private static readonly int[] BackoffMinutes = [1, 5, 30, 120, 360];

    protected override async Task ExecuteAsync(CancellationToken st)
    {
        var enabled = config.GetValue("InvoiceDispatch:Enabled", false);
        logger.LogInformation("InvoiceDispatchWorker: {Durum}", enabled ? "AKTİF" : "KAPALI (kuyruk birikir — gerçek entegratör adaptörüyle açılır)");
        var interval = TimeSpan.FromSeconds(Math.Max(15, config.GetValue("InvoiceDispatch:IntervalSeconds", 60)));
        while (!st.IsCancellationRequested)
        {
            try
            {
                if (config.GetValue("InvoiceDispatch:Enabled", false))
                    await KuyruguIsleAsync(st);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "InvoiceDispatchWorker döngü hatası");
            }
            await Task.Delay(interval, st);
        }
    }

    private async Task KuyruguIsleAsync(CancellationToken ct)
    {
        await using var lease = await workerLock.TryAcquireAsync("invoice-dispatch", ct);
        if (lease is null) return;

        using var scope = scopeFactory.CreateScope();
        var orderDb = scope.ServiceProvider.GetRequiredService<IOrderDbContext>();
        var intDb = scope.ServiceProvider.GetRequiredService<IIntegrationDbContext>();
        var firmResolver = scope.ServiceProvider.GetRequiredService<IFirmResolver>();
        var adapters = scope.ServiceProvider.GetRequiredService<IAdapterResolver>();

        var now = DateTime.UtcNow;
        var isler = await orderDb.InvoiceDispatches
            .Include(d => d.Invoice).ThenInclude(i => i.Items)
            .Where(d => d.Status == InvoiceDispatchStatuses.Pending && (d.NextAttemptAt == null || d.NextAttemptAt <= now))
            .OrderBy(d => d.CreatedAt)
            .Take(20)
            .ToListAsync(ct);
        if (isler.Count == 0) return;

        var sozlesmeler = (await firmResolver.GetEInvoiceContractsAsync(null, ct)).ToDictionary(c => c.Id);

        foreach (var d in isler)
        {
            var inv = d.Invoice;
            var contractId = d.IntegrationContractId ?? inv.IntegrationContractId;
            var basladi = DateTime.UtcNow;
            d.LastAttemptAt = basladi;

            if (contractId is null || !sozlesmeler.TryGetValue(contractId.Value, out var sozlesme))
            { Engelle(d, inv, "Entegratör sözleşmesi bulunamadı (seriye sözleşme bağlayın)."); continue; }
            if (!sozlesme.IsActive)
            { Engelle(d, inv, $"Entegratör sözleşmesi pasif ({sozlesme.ServiceCode})."); continue; }

            IEInvoiceAdapter adapter;
            try { adapter = adapters.GetEInvoiceAdapter(sozlesme.ServiceCode); }
            catch (InvalidOperationException e) { Engelle(d, inv, e.Message); continue; }
            if (adapter.IsStub && !sozlesme.TestMode)
            { Engelle(d, inv, $"{sozlesme.ServiceCode} adaptörü taslak (gerçek API'ye bağlı değil); canlı sözleşmede gönderim engellendi. Test için sözleşmede 'Test Modu' açın."); continue; }

            d.ProviderCode = sozlesme.ServiceCode;
            d.IntegrationContractId = contractId;
            var log = new IntegrationLog
            {
                FirmIntegrationId = contractId.Value, ServiceType = "einvoice",
                OperationType = d.Action == InvoiceDispatchActions.Cancel ? "cancel_invoice" : "send_invoice",
                ReferenceId = inv.Id, ReferenceType = "Invoice", Status = "pending"
            };
            try
            {
                EInvoiceResult sonuc;
                if (d.Action == InvoiceDispatchActions.Cancel)
                {
                    var uuid = inv.Ettn?.ToString() ?? inv.ExternalDocumentId
                        ?? throw new InvalidOperationException("İptal için entegratör belge kimliği (ETTN) yok.");
                    sonuc = await adapter.CancelInvoiceAsync(contractId.Value, uuid, "Sipariş iptali", ct);
                }
                else
                {
                    var currency = await orderDb.Orders.AsNoTracking().Where(o => o.Id == inv.OrderId)
                        .Select(o => o.CurrencyCode).FirstOrDefaultAsync(ct) ?? "TRY";
                    var payload = new EInvoicePayload(
                        inv.OrderId, inv.InvoiceNumber, inv.RecipientName, inv.RecipientTaxNumber ?? "",
                        inv.RecipientAddress, inv.GrandTotal, inv.TotalTax, currency, inv.InvoiceDate,
                        inv.Items.Select(i => new EInvoiceLineDto(
                            i.Description, (int)Math.Round(i.Quantity), i.UnitPrice, i.TaxRate, i.Total)).ToList());
                    d.RequestSnapshot = new Dictionary<string, object>
                    {
                        ["invoiceNumber"] = inv.InvoiceNumber, ["invoiceType"] = inv.InvoiceType,
                        ["grandTotal"] = inv.GrandTotal, ["lines"] = payload.Lines.Count, ["provider"] = sozlesme.ServiceCode,
                        ["testMode"] = sozlesme.TestMode
                    };
                    sonuc = await adapter.SendInvoiceAsync(contractId.Value, payload, ct);
                }

                log.DurationMs = (int)(DateTime.UtcNow - basladi).TotalMilliseconds;
                if (!sonuc.Success)
                    throw new InvalidOperationException(sonuc.ErrorMessage ?? "Entegratör hata döndü.");

                d.Status = InvoiceDispatchStatuses.Done;
                d.CompletedAt = DateTime.UtcNow;
                d.LastError = null;
                d.ResponseSnapshot = new Dictionary<string, object>
                    { ["invoiceUuid"] = sonuc.InvoiceUuid ?? "", ["stub"] = adapter.IsStub };
                log.Status = "success";
                log.ResponsePayload = sonuc.InvoiceUuid;

                if (d.Action == InvoiceDispatchActions.Cancel)
                {
                    inv.IntegratorStatus = "cancelled";
                }
                else
                {
                    inv.IntegratorStatus = "sent";
                    inv.IntegratorSentAt = DateTime.UtcNow;
                    inv.IntegratorResponse = d.ResponseSnapshot;
                    if (Guid.TryParse(sonuc.InvoiceUuid, out var ettn)) inv.Ettn ??= ettn;
                    inv.ExternalDocumentId ??= sonuc.InvoiceUuid;
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                d.Attempt++;
                d.LastError = e.Message.Length > 1900 ? e.Message[..1900] : e.Message;
                log.Status = "failure";
                log.ErrorMessage = d.LastError;
                log.DurationMs = (int)(DateTime.UtcNow - basladi).TotalMilliseconds;
                if (d.Attempt >= d.MaxAttempts)
                {
                    d.Status = InvoiceDispatchStatuses.Dead;
                    inv.IntegratorStatus = "error";
                }
                else
                {
                    d.NextAttemptAt = DateTime.UtcNow.AddMinutes(BackoffMinutes[Math.Min(d.Attempt - 1, BackoffMinutes.Length - 1)]);
                    inv.IntegratorStatus = "retrying";
                }
                logger.LogWarning("Fatura gönderimi başarısız ({No}, {Aksiyon}, deneme {N}/{Max}): {Hata}",
                    inv.InvoiceNumber, d.Action, d.Attempt, d.MaxAttempts, e.Message);
            }
            intDb.IntegrationLogs.Add(log);
        }

        await orderDb.SaveChangesAsync(ct);
        await intDb.SaveChangesAsync(ct);
    }

    private static void Engelle(InvoiceDispatch d, Invoice inv, string neden)
    {
        d.Status = InvoiceDispatchStatuses.Blocked;
        d.LastError = neden;
        inv.IntegratorStatus = "blocked";
    }
}
