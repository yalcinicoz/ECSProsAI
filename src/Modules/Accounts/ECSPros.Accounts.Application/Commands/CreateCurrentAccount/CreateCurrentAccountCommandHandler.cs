using ECSPros.Accounts.Application.Services;
using ECSPros.Accounts.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace ECSPros.Accounts.Application.Commands.CreateCurrentAccount;
public class CreateCurrentAccountCommandHandler : IRequestHandler<CreateCurrentAccountCommand, Result<Guid>>
{
    private readonly IAccountsDbContext _db;
    public CreateCurrentAccountCommandHandler(IAccountsDbContext db) => _db = db;
    public async Task<Result<Guid>> Handle(CreateCurrentAccountCommand request, CancellationToken ct)
    {
        if (await _db.CurrentAccounts.AnyAsync(a => a.Code == request.Code, ct))
            return Result.Failure<Guid>($"'{request.Code}' cari kodu zaten mevcut.");
        var account = new CurrentAccount
        {
            Code = request.Code, Title = request.Title, AccountType = request.AccountType,
            GroupId = request.GroupId, TaxNumber = request.TaxNumber, TaxOffice = request.TaxOffice,
            ContactName = request.ContactName, Phone = request.Phone, Email = request.Email,
            Address = request.Address, City = request.City, Country = request.Country ?? "TR",
            CreditLimit = request.CreditLimit, Currency = request.Currency, Notes = request.Notes,
        };
        _db.CurrentAccounts.Add(account);
        var ledger = new Domain.Entities.CurrentAccountLedger
        {
            CurrentAccountId = account.Id,
            Currency = account.Currency,
            Description = $"Varsayılan {account.Currency} hesabı",
            IsDefault = true,
            Balance = 0,
        };
        _db.AccountLedgers.Add(ledger);
        await _db.SaveChangesAsync(ct);
        return Result.Success(account.Id);
    }
}
