using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Services;

/// <summary>
/// Faz 0 stok atomikliği (docs/dayaniklilik-faz0-plani.md D2, AI analiz raporu §3.5):
/// stok mutasyonları VARYANT BAŞINA advisory kilitle serileştirilir. Desen:
///   açık transaction → (deadlock önlemek için SIRALI) pg_advisory_xact_lock(42901, hashtext(variantId))
///   → ChangeTracker temiz gövde (taze okuma) → SaveChanges → commit.
/// ExecutionStrategy sarması Faz 1'in EnableRetryOnFailure'ına hazırdır: yeniden denemede gövde
/// baştan çalışır; Clear() sayesinde önceki denemenin bellekte kalan artışları ikinci kez uygulanmaz.
/// Kilit anahtarı hash çakışması yalnız fazladan serileştirme yaratır (doğruluk bozulmaz).
/// </summary>
public static class StockTx
{
    private const int LockClass = 42901;   // stok kilit sınıfı (diğer advisory kullanımlarıyla çakışmasın)

    public static async Task RunAsync(
        IInventoryDbContext db, IEnumerable<Guid> variantIds, Func<Task> body, CancellationToken ct)
    {
        var ids = variantIds.Distinct().OrderBy(v => v).ToList();
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            foreach (var vid in ids)
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock({LockClass}, hashtext({vid.ToString()}))", ct);
            await body();
            await tx.CommitAsync(ct);
        });
    }
}
