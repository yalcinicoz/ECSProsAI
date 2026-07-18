using ECSPros.Accounts.Application.Queries.GetOwnerLedger;
using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Queries.GetMemberWallet;

public record GetMemberWalletQuery(Guid MemberId) : IRequest<Result<WalletDto>>;

public record WalletDto(
    Guid Id,
    Guid MemberId,
    decimal Balance,
    string CurrencyCode,
    List<WalletTransactionDto> RecentTransactions);

public record WalletTransactionDto(
    Guid Id,
    string TransactionType,
    decimal Debit,
    decimal Credit,
    decimal BalanceAfter,
    string? Description,
    DateTime CreatedAt);

/// <summary>
/// Cüzdan, Accounts modülündeki cari çatıdan okunur (OwnerType=member, ConceptCode=wallet).
/// Hesap henüz açılmamışsa (ilk harekete kadar açılmaz — lazy) 0 bakiyeli DTO döner.
/// Eski crm_wallets/crm_wallet_transactions tabloları DEPRECATED — hiç veri yazılmadı, okunmuyor.
/// </summary>
public class GetMemberWalletQueryHandler : IRequestHandler<GetMemberWalletQuery, Result<WalletDto>>
{
    private readonly ICrmDbContext _db;
    private readonly ISender _sender;

    public GetMemberWalletQueryHandler(ICrmDbContext db, ISender sender)
    {
        _db = db;
        _sender = sender;
    }

    public async Task<Result<WalletDto>> Handle(GetMemberWalletQuery request, CancellationToken ct)
    {
        var memberExists = await _db.Members.AnyAsync(m => m.Id == request.MemberId, ct);
        if (!memberExists)
            return Result.Failure<WalletDto>("Üye bulunamadı.");

        var ledger = await _sender.Send(
            new GetOwnerLedgerQuery("member", request.MemberId, "wallet"), ct);

        if (ledger.IsFailure)
            return Result.Success(new WalletDto(Guid.Empty, request.MemberId, 0, "TRY", new()));

        var v = ledger.Value!;
        return Result.Success(new WalletDto(
            v.LedgerId, request.MemberId, v.Balance, v.Currency,
            v.RecentTransactions.Select(t => new WalletTransactionDto(
                t.Id, t.TransactionType, t.Debit, t.Credit, t.BalanceAfter, t.Description, t.CreatedAt)).ToList()));
    }
}
