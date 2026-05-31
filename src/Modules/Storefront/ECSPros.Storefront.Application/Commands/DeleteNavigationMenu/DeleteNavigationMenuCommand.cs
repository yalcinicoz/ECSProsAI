using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.DeleteNavigationMenu;

public record DeleteNavigationMenuCommand(Guid Id) : IRequest<Result<bool>>;

public class DeleteNavigationMenuCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<DeleteNavigationMenuCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteNavigationMenuCommand request, CancellationToken ct)
    {
        var menu = await db.NavigationMenus.FirstOrDefaultAsync(m => m.Id == request.Id, ct);
        if (menu is null) return Result.Failure<bool>("Menü bulunamadı.");

        menu.IsDeleted = true;
        menu.DeletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
