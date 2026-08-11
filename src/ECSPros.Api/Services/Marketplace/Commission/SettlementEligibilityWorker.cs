using ECSPros.Accounts.Application.Commands.PostAccountTransaction;
using ECSPros.Accounts.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.Marketplace.Commission;

/// <summary>
/// P3a (2026-08-11): hakediş uygunlaşma worker'ı — EligibleAt'i geçmiş 'pending' satırları
/// satıcının 'hakedis' defterine PostAccountTransaction ile işler (pozitif net → Credit,
/// iade tersi negatif net → Debit, AllowNegativeBalance) ve satırı 'available' yapar.
/// Bakiye YALNIZ bu kapıdan değişir (cari çatı altın kuralı). 30 dakikada bir tarar;
/// satır başına hata diğerlerini durdurmaz.
/// </summary>
public sealed class SettlementEligibilityWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<SettlementEligibilityWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Periyot = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken st)
    {
        // Açılışta kısa gecikme — migrate/seed bitmeden sorgu atmayalım
        try { await Task.Delay(TimeSpan.FromSeconds(20), st); } catch { return; }

        while (!st.IsCancellationRequested)
        {
            try { await TaramaYap(st); }
            catch (Exception ex) { logger.LogError(ex, "Hakediş uygunlaşma taraması başarısız."); }
            try { await Task.Delay(Periyot, st); } catch { return; }
        }
    }

    private async Task TaramaYap(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAccountsDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var simdi = DateTime.UtcNow;
        var adaylar = await db.SettlementLines
            .Where(l => l.Status == "pending" && l.EligibleAt <= simdi)
            .OrderBy(l => l.EligibleAt)
            .Take(200)
            .ToListAsync(ct);
        if (adaylar.Count == 0) return;

        var islenen = 0;
        foreach (var satir in adaylar)
        {
            // Ters satır, orijinali DEFTERLENMEDEN işlenmez (borç öne geçmesin — sıra garantisi)
            if (satir.ReversalOfId is { } origId)
            {
                var orig = await db.SettlementLines.AsNoTracking().FirstOrDefaultAsync(l => l.Id == origId, ct);
                if (orig is not null && orig.Status == "pending") continue;
            }

            var tersMi = satir.NetAmount < 0;
            var tutar = Math.Abs(satir.NetAmount);
            Guid? txId = null;

            if (tutar > 0)
            {
                var sonuc = await mediator.Send(new PostAccountTransactionCommand(
                    OwnerType: "external", OwnerId: satir.SupplierAccountId,
                    ConceptCode: "hakedis",
                    TransactionType: tersMi ? "settlement_reversal" : "settlement_accrual",
                    Debit: tersMi ? tutar : 0,
                    Credit: tersMi ? 0 : tutar,
                    ReferenceType: "settlement_line", ReferenceId: satir.Id,
                    Description: $"{satir.OrderNumber} / {satir.Sku}" + (tersMi ? " (iade tersi)" : ""),
                    AllowNegativeBalance: true,
                    AccountId: satir.SupplierAccountId), ct);
                if (sonuc.IsFailure)
                {
                    logger.LogWarning("Hakediş defter kaydı başarısız ({Satir}): {Hata}", satir.Id, sonuc.Error);
                    continue; // satır pending kalır, sonraki taramada yeniden denenir
                }
                txId = sonuc.Value.TransactionId;
            }

            satir.Status = "available";
            satir.AvailableAt = simdi;
            satir.LedgerTransactionId = txId;
            // TAM iade tersi işlendiyse orijinali 'reversed' işaretle (kısmi iadede orijinal kalır)
            if (satir.ReversalOfId is { } orijinalId)
            {
                var orijinal = await db.SettlementLines.FirstOrDefaultAsync(l => l.Id == orijinalId, ct);
                if (orijinal is not null && orijinal.Status != "paid" && -satir.Quantity == orijinal.Quantity)
                    orijinal.Status = "reversed";
            }
            islenen++;
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Hakediş uygunlaşma: {Adet} satır defterlendi.", islenen);
    }
}
