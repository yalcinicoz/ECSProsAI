using ECSPros.Accounts.Application.Services;
using ECSPros.Accounts.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Accounts.Application.Commands.PostAccountTransaction;

/// <summary>
/// Tüm parasal kavramların (cüzdan, cari, depozito...) TEK hareket kapısı.
/// Hesap/defter yoksa lazy açar (üye hesabı kodu M-{sıra}); bakiyeyi advisory lock
/// altında atomik günceller. Bakiye elle başka hiçbir yerden değiştirilmemelidir.
/// </summary>
public record PostAccountTransactionCommand(
    string OwnerType,               // member | external | firm
    Guid OwnerId,
    string ConceptCode,             // wallet | cari | deposit ...
    string TransactionType,         // manual_adjustment | return_refund | payment ...
    decimal Debit,                  // bakiye azaltır
    decimal Credit,                 // bakiye artırır
    string? ReferenceType = null,
    Guid? ReferenceId = null,
    string? Description = null,
    string? OwnerTitle = null,      // hesap ilk açılışta kullanılacak ünvan
    bool AllowNegativeBalance = false,
    string Currency = "TRY",
    // P3a (2026-08-11): mevcut hesabı DOĞRUDAN hedefle (satıcı carileri OwnerId taşımaz —
    // owner çiftiyle bulunamaz). Dolu ise OwnerType/OwnerId yok sayılır ve lazy hesap AÇILMAZ.
    Guid? AccountId = null) : IRequest<Result<PostedTransactionDto>>;

public record PostedTransactionDto(Guid TransactionId, Guid LedgerId, Guid AccountId, decimal BalanceAfter);

public class PostAccountTransactionCommandHandler
    : IRequestHandler<PostAccountTransactionCommand, Result<PostedTransactionDto>>
{
    private readonly IAccountsDbContext _db;
    public PostAccountTransactionCommandHandler(IAccountsDbContext db) => _db = db;

    public async Task<Result<PostedTransactionDto>> Handle(PostAccountTransactionCommand r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.TransactionType))
            return Result.Failure<PostedTransactionDto>("Hareket tipi zorunludur.");
        if (r.Debit < 0 || r.Credit < 0)
            return Result.Failure<PostedTransactionDto>("Borç/alacak tutarı negatif olamaz.");
        if ((r.Debit > 0) == (r.Credit > 0))
            return Result.Failure<PostedTransactionDto>("Borç veya alacaktan yalnız biri sıfırdan büyük olmalıdır.");
        if (r.AccountId is null && r.OwnerId == Guid.Empty)
            return Result.Failure<PostedTransactionDto>("Hesap sahibi (OwnerId) zorunludur.");

        // Faz 1 (EnableRetryOnFailure): kullanıcı-transaction'ı ExecutionStrategy ile sarılmalı;
        // yeniden denemede gövde baştan çalışır (kilit + okuma + yazım hepsi içeride, idempotent).
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
        _db.ChangeTracker.Clear();
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Aynı sahibin aynı kavram defterine eşzamanlı yazımları serileştir
            var lockKey = r.AccountId is { } hedefId
                ? $"acc:{hedefId}:{r.ConceptCode}:{r.Currency}"
                : $"{r.OwnerType}:{r.OwnerId}:{r.ConceptCode}:{r.Currency}";
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 42))", ct);

            var account = r.AccountId is { } accountId
                ? await _db.CurrentAccounts.FirstOrDefaultAsync(a => a.Id == accountId, ct)
                : await _db.CurrentAccounts
                    .FirstOrDefaultAsync(a => a.OwnerType == r.OwnerType && a.OwnerId == r.OwnerId, ct);

            if (account is null && r.AccountId is not null)
                return Result.Failure<PostedTransactionDto>("Hedeflenen cari hesap bulunamadı.");

            if (account is null)
            {
                account = new CurrentAccount
                {
                    Code = await GenerateOwnerCodeAsync(r.OwnerType, ct),
                    Title = string.IsNullOrWhiteSpace(r.OwnerTitle) ? $"{r.OwnerType} {r.OwnerId:N}" : r.OwnerTitle!,
                    AccountType = "customer",
                    OwnerType = r.OwnerType,
                    OwnerId = r.OwnerId,
                    Currency = r.Currency,
                    IsActive = true
                };
                _db.CurrentAccounts.Add(account);
            }

            var ledger = await _db.AccountLedgers.FirstOrDefaultAsync(l =>
                l.CurrentAccountId == account.Id && l.ConceptCode == r.ConceptCode && l.Currency == r.Currency, ct);

            if (ledger is null)
            {
                ledger = new CurrentAccountLedger
                {
                    CurrentAccountId = account.Id,
                    ConceptCode = r.ConceptCode,
                    Currency = r.Currency,
                    Description = r.ConceptCode,
                    IsDefault = !await _db.AccountLedgers.AnyAsync(l => l.CurrentAccountId == account.Id, ct),
                    Balance = 0
                };
                _db.AccountLedgers.Add(ledger);
            }

            var newBalance = ledger.Balance + r.Credit - r.Debit;
            if (newBalance < 0 && !r.AllowNegativeBalance)
                return Result.Failure<PostedTransactionDto>(
                    $"Yetersiz bakiye: mevcut {ledger.Balance:0.00} {ledger.Currency}, istenen borç {r.Debit:0.00}.");

            var entry = new CurrentAccountTransaction
            {
                LedgerId = ledger.Id,
                TransactionType = r.TransactionType,
                Debit = r.Debit,
                Credit = r.Credit,
                BalanceAfter = newBalance,
                ReferenceType = r.ReferenceType,
                ReferenceId = r.ReferenceId,
                Description = r.Description,
                TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow)
            };
            ledger.Balance = newBalance;
            _db.AccountTransactions.Add(entry);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result.Success(new PostedTransactionDto(entry.Id, ledger.Id, account.Id, newBalance));
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(CancellationToken.None);
            return Result.Failure<PostedTransactionDto>("Hareket kaydedilemedi: " + ex.Message);
        }
        });
    }

    /// <summary>Üye hesapları M-{6 hane}; diğer sahip tipleri O-{6 hane}. Advisory lock ile serileşir.</summary>
    private async Task<string> GenerateOwnerCodeAsync(string ownerType, CancellationToken ct)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended('accounts:owner-code-seq', 42))", ct);
        var prefix = ownerType == "member" ? "M-" : "O-";
        var codes = await _db.CurrentAccounts.IgnoreQueryFilters()
            .Where(a => a.Code.StartsWith(prefix))
            .Select(a => a.Code)
            .ToListAsync(ct);
        var max = codes
            .Select(c => int.TryParse(c[prefix.Length..], out var n) ? n : 0)
            .DefaultIfEmpty(0).Max();
        return $"{prefix}{max + 1:D6}";
    }
}
