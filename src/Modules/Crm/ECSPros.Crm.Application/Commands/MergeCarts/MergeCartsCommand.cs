using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.MergeCarts;

public record MergeCartsCommand(
    string GuestSessionId,
    Guid MemberId,
    Guid FirmPlatformId) : IRequest<Result<Guid>>;

public class MergeCartsCommandHandler(ICrmDbContext db) : IRequestHandler<MergeCartsCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(MergeCartsCommand request, CancellationToken ct)
    {
        var guestCart = await db.Carts.Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.SessionId == request.GuestSessionId && c.FirmPlatformId == request.FirmPlatformId, ct);

        var memberCart = await db.Carts.Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.MemberId == request.MemberId && c.FirmPlatformId == request.FirmPlatformId, ct);

        if (guestCart is null || guestCart.Items.Count == 0)
            return Result.Success(memberCart?.Id ?? Guid.Empty);

        if (memberCart is null)
        {
            guestCart.MemberId = request.MemberId;
            guestCart.SessionId = null;
            guestCart.MergedFromCartId = guestCart.Id;
            guestCart.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Result.Success(guestCart.Id);
        }

        // Merge guest items into member cart
        foreach (var guestItem in guestCart.Items)
        {
            var existing = memberCart.Items.FirstOrDefault(i => i.VariantId == guestItem.VariantId);
            if (existing is not null)
                existing.Quantity += guestItem.Quantity;
            else
                memberCart.Items.Add(new CartItem
                {
                    CartId = memberCart.Id,
                    VariantId = guestItem.VariantId,
                    Quantity = guestItem.Quantity,
                    AddedPrice = guestItem.AddedPrice,
                    AddedAt = DateTime.UtcNow,
                    IsAvailable = guestItem.IsAvailable,
                    AvailableQuantity = guestItem.AvailableQuantity,
                    LastCheckedAt = DateTime.UtcNow
                });
        }

        guestCart.IsDeleted = true;
        memberCart.MergedFromCartId = guestCart.Id;
        memberCart.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result.Success(memberCart.Id);
    }
}
