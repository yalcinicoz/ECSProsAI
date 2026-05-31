using ECSPros.Accounts.Application.Services;
using ECSPros.Accounts.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace ECSPros.Accounts.Application.Commands.AddAccountLedger;
public class AddAccountLedgerCommandHandler : IRequestHandler<AddAccountLedgerCommand, Result<Guid>>
{
    private readonly IAccountsDbContext _db;
    public AddAccountLedgerCommandHandler(IAccountsDbContext db) => _db = db;
    public async Task<Result<Guid>> Handle(AddAccountLedgerCommand request, CancellationToken ct)
    {
        var accountExists = await _db.CurrentAccounts.AnyAsync(a => a.Id == request.CurrentAccountId, ct);
        if (!accountExists) return Result.Failure<Guid>("Cari bulunamadı.");
        var duplicate = await _db.AccountLedgers.AnyAsync(
            l => l.CurrentAccountId == request.CurrentAccountId && l.Currency == request.Currency, ct);
        if (duplicate) return Result.Failure<Guid>($"Bu cari için '{request.Currency}' hesabı zaten mevcut.");
        var ledger = new CurrentAccountLedger
        {
            CurrentAccountId = request.CurrentAccountId,
            Currency = request.Currency,
            Description = request.Description,
            IsDefault = false,
            Balance = 0,
        };
        _db.AccountLedgers.Add(ledger);
        await _db.SaveChangesAsync(ct);
        return Result.Success(ledger.Id);
    }
}
