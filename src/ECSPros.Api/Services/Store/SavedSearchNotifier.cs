using ECSPros.Catalog.Application.Queries.GetStoreProducts;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Infrastructure.Messaging;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.Store;

/// <summary>
/// H8: Favori arama bildirimi (E11'in bekleyen yarısı) — NotifyEnabled kayıtlı aramalar
/// için sorguya uyan YENİ ürün düştüyse e-posta atar. Pencere: LastNotifiedAt (hiç
/// bildirilmediyse son 24 saat) → şimdi; spam koruması LastNotifiedAt'la günde en fazla 1.
/// Gönderim başarısızsa LastNotifiedAt İLERLEMEZ (bir sonraki taramada yeniden denenir).
/// Tarama SavedSearchNotifyWorker'dan periyodik + admin endpoint'inden elle tetiklenir.
/// </summary>
public interface ISavedSearchNotifier
{
    /// <returns>Gönderilen bildirim sayısı.</returns>
    Task<int> RunOnceAsync(CancellationToken ct = default);
}

public class SavedSearchNotifier(
    IStorefrontDbContext storefrontDb,
    IMediator mediator,
    IMemberService memberService,
    IEmailService emailService,
    IStoreLinkBuilder linkBuilder,
    DistributedWorkerLock workerLock,
    ILogger<SavedSearchNotifier> logger) : ISavedSearchNotifier
{
    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        await using var lease = await workerLock.TryAcquireAsync("saved-search-notify", ct);
        if (lease is null) return 0;

        var esik = DateTime.UtcNow.AddHours(-24);
        var kayitlar = await storefrontDb.SavedSearches
            .Where(s => s.NotifyEnabled && (s.LastNotifiedAt == null || s.LastNotifiedAt < esik))
            .OrderBy(s => s.LastNotifiedAt)
            .Take(200) // tur başına üst sınır — kuyruk uzunsa sonraki tur devam eder
            .ToListAsync(ct);

        var gonderilen = 0;
        foreach (var kayit in kayitlar)
        {
            try
            {
                var pencereBasi = kayit.LastNotifiedAt ?? esik;
                var sonuc = await mediator.Send(new GetStoreProductsQuery(
                    kayit.FirmPlatformId, Search: kayit.Query, Page: 1, PageSize: 3,
                    Sort: "newest", CreatedSince: pencereBasi), ct);
                if (sonuc.IsFailure || !sonuc.Value!.Items.Any()) continue;

                var uye = await memberService.GetMemberAsync(kayit.MemberId, ct);
                if (string.IsNullOrWhiteSpace(uye?.Email)) continue;

                var aramaAdi = string.IsNullOrWhiteSpace(kayit.Name) ? kayit.Query : kayit.Name;
                var link = await linkBuilder.BuildAsync(
                    kayit.FirmPlatformId, "/urunler?search=" + Uri.EscapeDataString(kayit.Query), ct);
                var urunSatirlari = string.Join("", sonuc.Value.Items.Select(u =>
                    $"<li>{u.NameI18n.GetValueOrDefault("tr") ?? u.Code}</li>"));

                var govde = $"""
                    <div style="font-family:Arial,sans-serif;max-width:520px;margin:0 auto;color:#333">
                      <h2 style="font-size:18px">"{aramaAdi}" aramanıza yeni ürünler eklendi</h2>
                      <ul>{urunSatirlari}</ul>
                      {(link is null ? "" : $"""<p><a href="{link}" style="display:inline-block;background:#f27a1a;color:#fff;padding:10px 18px;border-radius:10px;text-decoration:none">Tümünü Gör</a></p>""")}
                      <p style="font-size:12px;color:#888">Bu e-posta, favori aramanızın bildirim tercihi açık olduğu için günde en fazla bir kez gönderilir. Tercihi Hesabım → Favori Aramalarım'dan kapatabilirsiniz.</p>
                    </div>
                    """;

                await emailService.SendAsync(uye.Email, $"Yeni ürünler: {aramaAdi}", govde, ct);
                kayit.LastNotifiedAt = DateTime.UtcNow;
                gonderilen++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Favori arama bildirimi gönderilemedi: {SearchId}", kayit.Id);
            }
        }

        if (gonderilen > 0)
            await storefrontDb.SaveChangesAsync(ct);

        return gonderilen;
    }
}

/// <summary>
/// H8: periyodik tarama — açılıştan kısa süre sonra ilk tur, sonra IntervalHours'ta bir.
/// Config: Store:SavedSearchNotify:{InitialDelaySeconds=120, IntervalHours=6}. Günde-1
/// sınırı LastNotifiedAt'ta olduğundan tur sıklığı yinelenen e-posta üretmez.
/// </summary>
public class SavedSearchNotifyWorker(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<SavedSearchNotifyWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var ilkGecikme = TimeSpan.FromSeconds(configuration.GetValue("Store:SavedSearchNotify:InitialDelaySeconds", 120));
        var aralik = TimeSpan.FromHours(configuration.GetValue("Store:SavedSearchNotify:IntervalHours", 6));

        try { await Task.Delay(ilkGecikme, stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var sayi = await scope.ServiceProvider.GetRequiredService<ISavedSearchNotifier>()
                    .RunOnceAsync(stoppingToken);
                if (sayi > 0)
                    logger.LogInformation("Favori arama taraması: {Sayi} bildirim gönderildi.", sayi);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Favori arama tarama turu hata verdi — sonraki turda denenecek.");
            }

            try { await Task.Delay(aralik, stoppingToken); } catch (OperationCanceledException) { return; }
        }
    }
}
