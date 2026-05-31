using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.UpdateNavigationMenu;

public record UpdateNavigationMenuCommand(
    Guid Id,
    Dictionary<string, string> NameI18n,
    string MenuType,
    bool IsActive,
    int SortOrder) : IRequest<Result<bool>>;

public class UpdateNavigationMenuCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<UpdateNavigationMenuCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateNavigationMenuCommand request, CancellationToken ct)
    {
        var menu = await db.NavigationMenus.FirstOrDefaultAsync(m => m.Id == request.Id, ct);
        if (menu is null) return Result.Failure<bool>("Menü bulunamadı.");

        menu.NameI18n = request.NameI18n;
        menu.MenuType = request.MenuType;
        menu.IsActive = request.IsActive;
        menu.SortOrder = request.SortOrder;

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
