using ECSPros.Accounts.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace ECSPros.Accounts.Application.Commands.UpdateAccountGroup;
public class UpdateAccountGroupCommandHandler : IRequestHandler<UpdateAccountGroupCommand, Result>
{
    private readonly IAccountsDbContext _db;
    public UpdateAccountGroupCommandHandler(IAccountsDbContext db) => _db = db;
    public async Task<Result> Handle(UpdateAccountGroupCommand request, CancellationToken ct)
    {
        var group = await _db.AccountGroups.FirstOrDefaultAsync(g => g.Id == request.Id, ct);
        if (group is null) return Result.Failure("Grup bulunamadı.");
        group.Name = request.Name; group.GroupType = request.GroupType;
        group.Description = request.Description; group.SortOrder = request.SortOrder;
        group.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
