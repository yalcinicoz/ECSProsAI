using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Commands.UpdateAdminMenu;

public record UpdateAdminMenuCommand(
    Guid Id,
    Dictionary<string, string> NameI18n,
    string? Icon,
    string? Route,
    string? PermissionCode,
    int SortOrder,
    bool IsActive
) : IRequest<Result<bool>>;

public class UpdateAdminMenuCommandHandler : IRequestHandler<UpdateAdminMenuCommand, Result<bool>>
{
    private readonly IIamDbContext _db;

    public UpdateAdminMenuCommandHandler(IIamDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(UpdateAdminMenuCommand request, CancellationToken ct)
    {
        var menu = await _db.AdminMenus.FirstOrDefaultAsync(m => m.Id == request.Id, ct);
        if (menu is null)
            return Result.Failure<bool>("Menü öğesi bulunamadı.");

        menu.NameI18n = request.NameI18n;
        menu.Icon = request.Icon;
        menu.Route = request.Route;
        menu.PermissionCode = request.PermissionCode;
        menu.SortOrder = request.SortOrder;
        menu.IsActive = request.IsActive;
        menu.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
