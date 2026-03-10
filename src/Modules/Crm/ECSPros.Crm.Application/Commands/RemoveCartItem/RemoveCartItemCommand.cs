using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.RemoveCartItem;

public record RemoveCartItemCommand(Guid CartId, Guid ItemId) : IRequest<Result<bool>>;

public class RemoveCartItemCommandHandler(ICrmDbContext db) : IRequestHandler<RemoveCartItemCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(RemoveCartItemCommand request, CancellationToken ct)
    {
        var item = await db.CartItems.FirstOrDefaultAsync(i => i.Id == request.ItemId && i.CartId == request.CartId, ct);
        if (item is null) return Result.Failure<bool>("Sepet kalemi bulunamadı.");
        db.CartItems.Remove(item);
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
