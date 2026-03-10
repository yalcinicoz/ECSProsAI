using ECSPros.Iam.Application.Services;
using ECSPros.Iam.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Commands.CreateAdminMenu;

public record CreateAdminMenuCommand(
    Guid? ParentId,
    string Code,
    Dictionary<string, string> NameI18n,
    string? Icon,
    string? Route,
    string? PermissionCode,
    int SortOrder
) : IRequest<Result<Guid>>;

public class CreateAdminMenuCommandHandler : IRequestHandler<CreateAdminMenuCommand, Result<Guid>>
{
    private readonly IIamDbContext _db;

    public CreateAdminMenuCommandHandler(IIamDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateAdminMenuCommand request, CancellationToken ct)
    {
        if (request.ParentId.HasValue)
        {
            var parentExists = await _db.AdminMenus.AnyAsync(m => m.Id == request.ParentId, ct);
            if (!parentExists)
                return Result.Failure<Guid>("Üst menü bulunamadı.");
        }

        var menu = new AdminMenu
        {
            Id = Guid.NewGuid(),
            ParentId = request.ParentId,
            Code = request.Code.Trim(),
            NameI18n = request.NameI18n,
            Icon = request.Icon,
            Route = request.Route,
            PermissionCode = request.PermissionCode,
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.AdminMenus.Add(menu);
        await _db.SaveChangesAsync(ct);

        return Result.Success(menu.Id);
    }
}
