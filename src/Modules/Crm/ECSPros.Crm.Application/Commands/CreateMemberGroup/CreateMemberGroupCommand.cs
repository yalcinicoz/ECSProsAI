using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.CreateMemberGroup;

public record CreateMemberGroupCommand(
    string Code,
    Dictionary<string, string> NameI18n,
    bool IsDefault,
    bool IsWholesale,
    bool RequiresApproval,
    bool ShowPricesBeforeLogin,
    decimal? MinOrderAmount,
    int? PaymentTermsDays,
    int SortOrder
) : IRequest<Result<Guid>>;

public class CreateMemberGroupCommandHandler : IRequestHandler<CreateMemberGroupCommand, Result<Guid>>
{
    private readonly ICrmDbContext _db;

    public CreateMemberGroupCommandHandler(ICrmDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateMemberGroupCommand request, CancellationToken ct)
    {
        var codeExists = await _db.MemberGroups.AnyAsync(g => g.Code == request.Code, ct);
        if (codeExists)
            return Result.Failure<Guid>("Bu kod ile bir üye grubu zaten mevcut.");

        var group = new MemberGroup
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            NameI18n = request.NameI18n,
            IsDefault = request.IsDefault,
            IsWholesale = request.IsWholesale,
            RequiresApproval = request.RequiresApproval,
            ShowPricesBeforeLogin = request.ShowPricesBeforeLogin,
            MinOrderAmount = request.MinOrderAmount,
            PaymentTermsDays = request.PaymentTermsDays,
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.MemberGroups.Add(group);
        await _db.SaveChangesAsync(ct);

        return Result.Success(group.Id);
    }
}
