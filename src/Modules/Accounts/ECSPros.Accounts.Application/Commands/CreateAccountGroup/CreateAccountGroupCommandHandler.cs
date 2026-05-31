using ECSPros.Accounts.Application.Services;
using ECSPros.Accounts.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace ECSPros.Accounts.Application.Commands.CreateAccountGroup;
public class CreateAccountGroupCommandHandler : IRequestHandler<CreateAccountGroupCommand, Result<Guid>>
{
    private readonly IAccountsDbContext _db;
    public CreateAccountGroupCommandHandler(IAccountsDbContext db) => _db = db;
    public async Task<Result<Guid>> Handle(CreateAccountGroupCommand request, CancellationToken ct)
    {
        if (await _db.AccountGroups.AnyAsync(g => g.Code == request.Code, ct))
            return Result.Failure<Guid>($"'{request.Code}' kodu zaten mevcut.");
        var group = new CurrentAccountGroup
        {
            Code = request.Code, Name = request.Name, GroupType = request.GroupType,
            Description = request.Description, SortOrder = request.SortOrder,
        };
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync(ct);
        return Result.Success(group.Id);
    }
}
