using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.DeleteCartRemovedItem;

/// <summary>2026-07-17: "Önceden Eklediklerim" kaydını düşürür — listeden elle kaldırma
/// veya ürün sepete geri eklendiğinde. Kayıt yoksa da başarı döner (idempotent).</summary>
public record DeleteCartRemovedItemCommand(
    Guid FirmPlatformId,
    Guid MemberId,
    Guid VariantId) : IRequest<Result<bool>>;

public class DeleteCartRemovedItemCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<DeleteCartRemovedItemCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteCartRemovedItemCommand request, CancellationToken ct)
    {
        var kayit = await db.CartRemovedItems.FirstOrDefaultAsync(
            x => x.FirmPlatformId == request.FirmPlatformId
                 && x.MemberId == request.MemberId
                 && x.VariantId == request.VariantId, ct);
        if (kayit is null)
            return Result.Success(true);

        kayit.IsDeleted = true;
        kayit.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
