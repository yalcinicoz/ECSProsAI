using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.ClearCart;

public record ClearCartCommand(Guid CartId) : IRequest<Result<bool>>;

public class ClearCartCommandHandler(ICrmDbContext db) : IRequestHandler<ClearCartCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(ClearCartCommand request, CancellationToken ct)
    {
        var items = db.CartItems.Where(i => i.CartId == request.CartId);
        db.CartItems.RemoveRange(items);
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
