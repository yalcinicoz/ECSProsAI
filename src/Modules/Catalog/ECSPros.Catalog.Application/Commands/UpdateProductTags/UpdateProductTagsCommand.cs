using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.UpdateProductTags;

public record UpdateProductTagsCommand(Guid ProductId, List<string> Tags) : IRequest<Result<bool>>;

public class UpdateProductTagsCommandHandler : IRequestHandler<UpdateProductTagsCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;
    public UpdateProductTagsCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(UpdateProductTagsCommand request, CancellationToken ct)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, ct);
        if (product is null) return Result.Failure<bool>("Ürün bulunamadı.");

        product.Tags = request.Tags
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct()
            .ToList();
        product.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
