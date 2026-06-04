using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.RemoveChannelCategoryProduct;

public record RemoveChannelCategoryProductCommand(
    Guid ChannelCategoryId,
    Guid ProductId) : IRequest<Result<bool>>;

public class RemoveChannelCategoryProductCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<RemoveChannelCategoryProductCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(RemoveChannelCategoryProductCommand request, CancellationToken ct)
    {
        var entry = await db.ChannelCategoryProducts
            .FirstOrDefaultAsync(p => p.ChannelCategoryId == request.ChannelCategoryId
                                   && p.ProductId == request.ProductId, ct);

        if (entry is null) return Result.Failure<bool>("Kayıt bulunamadı.");

        db.ChannelCategoryProducts.Remove(entry);
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
