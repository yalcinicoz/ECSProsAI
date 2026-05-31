using ECSPros.Accounts.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace ECSPros.Accounts.Application.Commands.UpdateCurrentAccount;
public class UpdateCurrentAccountCommandHandler : IRequestHandler<UpdateCurrentAccountCommand, Result>
{
    private readonly IAccountsDbContext _db;
    public UpdateCurrentAccountCommandHandler(IAccountsDbContext db) => _db = db;
    public async Task<Result> Handle(UpdateCurrentAccountCommand request, CancellationToken ct)
    {
        var a = await _db.CurrentAccounts.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (a is null) return Result.Failure("Cari bulunamadı.");
        a.Title = request.Title; a.AccountType = request.AccountType; a.GroupId = request.GroupId;
        a.TaxNumber = request.TaxNumber; a.TaxOffice = request.TaxOffice; a.ContactName = request.ContactName;
        a.Phone = request.Phone; a.Email = request.Email; a.Address = request.Address;
        a.City = request.City; a.Country = request.Country; a.CreditLimit = request.CreditLimit;
        a.Currency = request.Currency; a.Notes = request.Notes; a.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
