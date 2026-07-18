using ECSPros.Accounts.Application.Queries.GetOwnerLedger;
using ECSPros.Accounts.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Accounts.Application.Queries.GetAccountTransactions;

/// <summary>Cari kart detayı için: hesabın defterleri + (opsiyonel tek deftere filtreli) sayfalı hareket dökümü.</summary>
public record GetAccountTransactionsQuery(
    Guid AccountId,
    Guid? LedgerId = null,
    int Page = 1,
    int PageSize = 50) : IRequest<Result<AccountTransactionsDto>>;

public record AccountLedgerSummaryDto(Guid Id, string ConceptCode, string Currency, decimal Balance, bool IsDefault);

public record AccountTransactionsDto(
    List<AccountLedgerSummaryDto> Ledgers,
    List<LedgerTransactionDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public class GetAccountTransactionsQueryHandler
    : IRequestHandler<GetAccountTransactionsQuery, Result<AccountTransactionsDto>>
{
    private readonly IAccountsDbContext _db;
    public GetAccountTransactionsQueryHandler(IAccountsDbContext db) => _db = db;

    public async Task<Result<AccountTransactionsDto>> Handle(GetAccountTransactionsQuery r, CancellationToken ct)
    {
        var exists = await _db.CurrentAccounts.AnyAsync(a => a.Id == r.AccountId, ct);
        if (!exists)
            return Result.Failure<AccountTransactionsDto>("Cari hesap bulunamadı.");

        var ledgers = await _db.AccountLedgers
            .Where(l => l.CurrentAccountId == r.AccountId)
            .OrderByDescending(l => l.IsDefault).ThenBy(l => l.ConceptCode)
            .Select(l => new AccountLedgerSummaryDto(l.Id, l.ConceptCode, l.Currency, l.Balance, l.IsDefault))
            .ToListAsync(ct);

        var ledgerIds = r.LedgerId.HasValue
            ? ledgers.Where(l => l.Id == r.LedgerId.Value).Select(l => l.Id).ToList()
            : ledgers.Select(l => l.Id).ToList();

        var page = Math.Max(1, r.Page);
        var size = Math.Clamp(r.PageSize, 1, 200);
        var query = _db.AccountTransactions.Where(t => ledgerIds.Contains(t.LedgerId));
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * size).Take(size)
            .Select(t => new LedgerTransactionDto(
                t.Id, t.TransactionType, t.Debit, t.Credit, t.BalanceAfter,
                t.ReferenceType, t.ReferenceId, t.Description, t.CreatedAt))
            .ToListAsync(ct);

        return Result.Success(new AccountTransactionsDto(ledgers, items, total, page, size));
    }
}
