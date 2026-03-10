using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.UpdateMemberGroup;

public record UpdateMemberGroupCommand(
    Guid Id,
    Dictionary<string, string> NameI18n,
    bool IsWholesale,
    bool RequiresApproval,
    bool ShowPricesBeforeLogin,
    decimal? MinOrderAmount,
    int? PaymentTermsDays,
    bool IsActive,
    int SortOrder
) : IRequest<Result<bool>>;

public class UpdateMemberGroupCommandHandler : IRequestHandler<UpdateMemberGroupCommand, Result<bool>>
{
    private readonly ICrmDbContext _db;

    public UpdateMemberGroupCommandHandler(ICrmDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(UpdateMemberGroupCommand request, CancellationToken ct)
    {
        var group = await _db.MemberGroups.FirstOrDefaultAsync(g => g.Id == request.Id, ct);
        if (group is null)
            return Result.Failure<bool>("Üye grubu bulunamadı.");

        group.NameI18n = request.NameI18n;
        group.IsWholesale = request.IsWholesale;
        group.RequiresApproval = request.RequiresApproval;
        group.ShowPricesBeforeLogin = request.ShowPricesBeforeLogin;
        group.MinOrderAmount = request.MinOrderAmount;
        group.PaymentTermsDays = request.PaymentTermsDays;
        group.IsActive = request.IsActive;
        group.SortOrder = request.SortOrder;
        group.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
