using ECSPros.Accounts.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Accounts.Application.Queries.GetOwnerLedger;

/// <summary>Sahip (üye vb.) + kavram defterinin bakiyesi ve son hareketleri. Hesap yoksa Failure döner —
/// çağıran taraf boş durumu (0 bakiye) kendisi kurgular.</summary>
public record GetOwnerLedgerQuery(
    string OwnerType,
    Guid OwnerId,
    string ConceptCode,
    string Currency = "TRY",
    int RecentCount = 20) : IRequest<Result<OwnerLedgerDto>>;

public record OwnerLedgerDto(
    Guid LedgerId,
    Guid AccountId,
    string AccountCode,
    decimal Balance,
    string Currency,
    List<LedgerTransactionDto> RecentTransactions);

public record LedgerTransactionDto(
    Guid Id,
    string TransactionType,
    decimal Debit,
    decimal Credit,
    decimal BalanceAfter,
    string? ReferenceType,
    Guid? ReferenceId,
    string? Description,
    DateTime CreatedAt);

public class GetOwnerLedgerQueryHandler : IRequestHandler<GetOwnerLedgerQuery, Result<OwnerLedgerDto>>
{
    private readonly IAccountsDbContext _db;
    public GetOwnerLedgerQueryHandler(IAccountsDbContext db) => _db = db;

    public async Task<Result<OwnerLedgerDto>> Handle(GetOwnerLedgerQuery r, CancellationToken ct)
    {
        var row = await (
            from l in _db.AccountLedgers
            join a in _db.CurrentAccounts on l.CurrentAccountId equals a.Id
            where a.OwnerType == r.OwnerType && a.OwnerId == r.OwnerId
                  && l.ConceptCode == r.ConceptCode && l.Currency == r.Currency
            select new { l.Id, AccountId = a.Id, a.Code, l.Balance, l.Currency })
            .FirstOrDefaultAsync(ct);

        if (row is null)
            return Result.Failure<OwnerLedgerDto>("Hesap bulunamadı.");

        var txs = await _db.AccountTransactions
            .Where(t => t.LedgerId == row.Id)
            .OrderByDescending(t => t.CreatedAt)
            .Take(r.RecentCount)
            .Select(t => new LedgerTransactionDto(
                t.Id, t.TransactionType, t.Debit, t.Credit, t.BalanceAfter,
                t.ReferenceType, t.ReferenceId, t.Description, t.CreatedAt))
            .ToListAsync(ct);

        return Result.Success(new OwnerLedgerDto(row.Id, row.AccountId, row.Code, row.Balance, row.Currency, txs));
    }
}
