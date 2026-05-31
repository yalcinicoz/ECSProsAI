using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.AddCategoryProduct;

public record AddCategoryProductCommand(
    Guid CategoryId,
    Guid ProductId,
    int SortOrder = 0,
    bool IsPinned = false) : IRequest<Result<bool>>;

public class AddCategoryProductCommandHandler(ICatalogDbContext db)
    : IRequestHandler<AddCategoryProductCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(AddCategoryProductCommand request, CancellationToken ct)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == request.CategoryId, ct);
        if (category is null) return Result.Failure<bool>("Kategori bulunamadı.");

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, ct);
        if (product is null) return Result.Failure<bool>("Ürün bulunamadı.");

        var exists = await db.CategoryProducts
            .AnyAsync(cp => cp.CategoryId == request.CategoryId && cp.ProductId == request.ProductId, ct);
        if (exists) return Result.Failure<bool>("Bu ürün zaten kategoride mevcut.");

        db.CategoryProducts.Add(new CategoryProduct
        {
            Id = Guid.NewGuid(),
            CategoryId = request.CategoryId,
            ProductId = request.ProductId,
            SortOrder = request.SortOrder,
            IsPinned = request.IsPinned,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
