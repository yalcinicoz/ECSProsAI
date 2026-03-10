using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.UpdateCartItem;

public record UpdateCartItemCommand(Guid CartId, Guid ItemId, int Quantity) : IRequest<Result<bool>>;

public class UpdateCartItemCommandHandler(ICrmDbContext db) : IRequestHandler<UpdateCartItemCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateCartItemCommand request, CancellationToken ct)
    {
        var item = await db.CartItems.FirstOrDefaultAsync(i => i.Id == request.ItemId && i.CartId == request.CartId, ct);
        if (item is null) return Result.Failure<bool>("Sepet kalemi bulunamadı.");

        if (request.Quantity <= 0)
        {
            db.CartItems.Remove(item);
        }
        else
        {
            item.Quantity = request.Quantity;
            item.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
