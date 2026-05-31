using ECSPros.Accounts.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace ECSPros.Accounts.Application.Queries.GetCurrentAccountDetail;

public record GetCurrentAccountDetailQuery(Guid Id) : IRequest<Result<CurrentAccountDetailDto>>;

public record LedgerDto(Guid Id, string Currency, string? Description, bool IsDefault, decimal Balance, DateTime CreatedAt);

public record CurrentAccountDetailDto(
    Guid Id, string Code, string Title, string AccountType,
    Guid? GroupId, string? GroupName, string? GroupCode,
    string? TaxNumber, string? TaxOffice, string? ContactName,
    string? Phone, string? Email, string? Address, string? City, string? Country,
    decimal CreditLimit, string Currency, string? Notes,
    bool IsActive, DateTime CreatedAt, DateTime? UpdatedAt,
    List<LedgerDto> Ledgers);

public class GetCurrentAccountDetailQueryHandler : IRequestHandler<GetCurrentAccountDetailQuery, Result<CurrentAccountDetailDto>>
{
    private readonly IAccountsDbContext _db;
    public GetCurrentAccountDetailQueryHandler(IAccountsDbContext db) => _db = db;
    public async Task<Result<CurrentAccountDetailDto>> Handle(GetCurrentAccountDetailQuery request, CancellationToken ct)
    {
        var a = await _db.CurrentAccounts.Include(x => x.Group)
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (a is null) return Result.Failure<CurrentAccountDetailDto>("Cari bulunamadı.");

        var ledgers = await _db.AccountLedgers
            .Where(l => l.CurrentAccountId == a.Id)
            .OrderByDescending(l => l.IsDefault).ThenBy(l => l.Currency)
            .Select(l => new LedgerDto(l.Id, l.Currency, l.Description, l.IsDefault, l.Balance, l.CreatedAt))
            .ToListAsync(ct);

        return Result.Success(new CurrentAccountDetailDto(
            a.Id, a.Code, a.Title, a.AccountType,
            a.GroupId, a.Group?.Name, a.Group?.Code,
            a.TaxNumber, a.TaxOffice, a.ContactName,
            a.Phone, a.Email, a.Address, a.City, a.Country,
            a.CreditLimit, a.Currency, a.Notes,
            a.IsActive, a.CreatedAt, a.UpdatedAt,
            ledgers));
    }
}
