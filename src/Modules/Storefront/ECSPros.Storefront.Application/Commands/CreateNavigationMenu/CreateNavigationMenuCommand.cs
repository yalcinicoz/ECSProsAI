using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.CreateNavigationMenu;

public record CreateNavigationMenuCommand(
    Guid FirmPlatformId,
    string Code,
    Dictionary<string, string> NameI18n,
    string MenuType = "header",
    int SortOrder = 0) : IRequest<Result<Guid>>;

public class CreateNavigationMenuCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<CreateNavigationMenuCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateNavigationMenuCommand request, CancellationToken ct)
    {
        var exists = await db.NavigationMenus.AnyAsync(
            m => m.FirmPlatformId == request.FirmPlatformId && m.Code == request.Code, ct);
        if (exists) return Result.Failure<Guid>("Bu kanal ve kod için menü zaten mevcut.");

        var menu = new NavigationMenu
        {
            Id = Guid.NewGuid(),
            FirmPlatformId = request.FirmPlatformId,
            Code = request.Code,
            NameI18n = request.NameI18n,
            MenuType = request.MenuType,
            SortOrder = request.SortOrder,
            IsActive = true
        };

        db.NavigationMenus.Add(menu);
        await db.SaveChangesAsync(ct);
        return Result.Success(menu.Id);
    }
}
