using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.AddChannelCategoryProduct;

public record AddChannelCategoryProductCommand(
    Guid ChannelCategoryId,
    Guid ProductId,
    int SortOrder,
    bool IsExcluded) : IRequest<Result<bool>>;

public class AddChannelCategoryProductCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<AddChannelCategoryProductCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(AddChannelCategoryProductCommand request, CancellationToken ct)
    {
        var exists = await db.ChannelCategoryProducts
            .AnyAsync(p => p.ChannelCategoryId == request.ChannelCategoryId
                        && p.ProductId == request.ProductId, ct);
        if (exists) return Result.Failure<bool>("Bu ürün kategoride zaten mevcut.");

        db.ChannelCategoryProducts.Add(new ChannelCategoryProduct
        {
            ChannelCategoryId = request.ChannelCategoryId,
            ProductId         = request.ProductId,
            SortOrder         = request.SortOrder,
            IsExcluded        = request.IsExcluded,
        });

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
