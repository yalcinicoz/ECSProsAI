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

public class GetMemberWalletQueryHandler : IRequestHandler<GetMemberWalletQuery, Result<WalletDto>>
{
    private readonly ICrmDbContext _db;

    public GetMemberWalletQueryHandler(ICrmDbContext db) => _db = db;

    public async Task<Result<WalletDto>> Handle(GetMemberWalletQuery request, CancellationToken ct)
    {
        var wallet = await _db.Wallets
            .Include(w => w.Transactions.OrderByDescending(t => t.CreatedAt).Take(20))
            .FirstOrDefaultAsync(w => w.MemberId == request.MemberId, ct);

        if (wallet is null)
            return Result.Failure<WalletDto>("Cüzdan bulunamadı.");

        var dto = new WalletDto(
            wallet.Id, wallet.MemberId, wallet.Balance, wallet.CurrencyCode,
            wallet.Transactions.Select(t => new WalletTransactionDto(
                t.Id, t.TransactionType, t.Debit, t.Credit, t.BalanceAfter, t.Description, t.CreatedAt)).ToList());

        return Result.Success(dto);
    }
}
