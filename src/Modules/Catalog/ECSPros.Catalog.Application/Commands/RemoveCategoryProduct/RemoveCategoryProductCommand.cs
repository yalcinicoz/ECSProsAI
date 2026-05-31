using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.RemoveCategoryProduct;

public record RemoveCategoryProductCommand(Guid CategoryId, Guid ProductId) : IRequest<Result<bool>>;

public class RemoveCategoryProductCommandHandler(ICatalogDbContext db)
    : IRequestHandler<RemoveCategoryProductCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(RemoveCategoryProductCommand request, CancellationToken ct)
    {
        var entry = await db.CategoryProducts
            .FirstOrDefaultAsync(cp => cp.CategoryId == request.CategoryId && cp.ProductId == request.ProductId, ct);

        if (entry is null) return Result.Failure<bool>("Kategori-ürün ilişkisi bulunamadı.");

        entry.IsDeleted = true;
        entry.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
