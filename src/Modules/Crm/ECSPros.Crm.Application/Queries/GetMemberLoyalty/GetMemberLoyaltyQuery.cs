using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Queries.GetMemberLoyalty;

public record GetMemberLoyaltyQuery(Guid MemberId) : IRequest<Result<LoyaltyDto>>;

public record LoyaltyDto(
    Guid Id,
    Guid MemberId,
    int TotalPoints,
    int AvailablePoints,
    int PendingPoints,
    decimal PointsToCurrencyRate,
    List<LoyaltyTransactionDto> RecentTransactions);

public record LoyaltyTransactionDto(
    Guid Id,
    string TransactionType,
    int Points,
    int BalanceAfter,
    string? Notes,
    DateTime? ExpiresAt,
    DateTime CreatedAt);

public class GetMemberLoyaltyQueryHandler : IRequestHandler<GetMemberLoyaltyQuery, Result<LoyaltyDto>>
{
    private readonly ICrmDbContext _db;

    public GetMemberLoyaltyQueryHandler(ICrmDbContext db) => _db = db;

    public async Task<Result<LoyaltyDto>> Handle(GetMemberLoyaltyQuery request, CancellationToken ct)
    {
        var account = await _db.LoyaltyAccounts
            .Include(a => a.Transactions.OrderByDescending(t => t.CreatedAt).Take(20))
            .FirstOrDefaultAsync(a => a.MemberId == request.MemberId, ct);

        if (account is null)
            return Result.Failure<LoyaltyDto>("Sadakat hesabı bulunamadı.");

        var dto = new LoyaltyDto(
            account.Id, account.MemberId,
            account.TotalPoints, account.AvailablePoints, account.PendingPoints,
            account.PointsToCurrencyRate,
            account.Transactions.Select(t => new LoyaltyTransactionDto(
                t.Id, t.TransactionType, t.Points, t.BalanceAfter, t.Notes, t.ExpiresAt, t.CreatedAt)).ToList());

        return Result.Success(dto);
    }
}
