using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.SyncCategoryProducts;

/// <summary>
/// Global katalog kategorisinden silinmiş ürünleri temizler.
/// Filtre tabanlı kategori mantığı artık ChannelCategory'de.
/// </summary>
public record SyncCategoryProductsCommand(Guid CategoryId) : IRequest<Result<int>>;

public class SyncCategoryProductsCommandHandler(ICatalogDbContext db)
    : IRequestHandler<SyncCategoryProductsCommand, Result<int>>
{
    public async Task<Result<int>> Handle(SyncCategoryProductsCommand request, CancellationToken ct)
    {
        var category = await db.Categories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, ct);

        if (category is null) return Result.Failure<int>("Kategori bulunamadı.");

        var categoryProducts = await db.CategoryProducts
            .Where(cp => cp.CategoryId == request.CategoryId)
            .Include(cp => cp.Product)
            .ToListAsync(ct);

        var toRemove = categoryProducts.Where(cp => cp.Product.IsDeleted).ToList();
        foreach (var cp in toRemove)
        {
            cp.IsDeleted = true;
            cp.DeletedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return Result.Success(toRemove.Count);
    }
}
