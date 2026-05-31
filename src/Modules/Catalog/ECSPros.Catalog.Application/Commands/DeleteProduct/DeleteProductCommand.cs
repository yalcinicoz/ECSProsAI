using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.DeleteProduct;

public record DeleteProductCommand(Guid ProductId, Guid DeletedBy) : IRequest<Result<bool>>;

public class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public DeleteProductCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(DeleteProductCommand request, CancellationToken ct)
    {
        var product = await _db.Products
            .Include(p => p.Variants)
            .Include(p => p.Attributes)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, ct);

        if (product is null)
            return Result.Failure<bool>("Ürün bulunamadı.");

        var now = DateTime.UtcNow;

        foreach (var variant in product.Variants)
        {
            variant.IsDeleted  = true;
            variant.DeletedAt  = now;
            variant.DeletedBy  = request.DeletedBy;
        }

        foreach (var attr in product.Attributes)
        {
            attr.IsDeleted = true;
            attr.DeletedAt = now;
            attr.DeletedBy = request.DeletedBy;
        }

        product.IsDeleted = true;
        product.DeletedAt = now;
        product.DeletedBy = request.DeletedBy;

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
