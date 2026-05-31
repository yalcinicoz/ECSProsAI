using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.UpdateProductSeo;

public record UpdateProductSeoCommand(
    Guid ProductId,
    string? Slug,
    Dictionary<string, string>? MetaTitleI18n,
    Dictionary<string, string>? MetaDescriptionI18n,
    Dictionary<string, string>? MetaKeywordsI18n
) : IRequest<Result<bool>>;

public class UpdateProductSeoCommandHandler : IRequestHandler<UpdateProductSeoCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;
    public UpdateProductSeoCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(UpdateProductSeoCommand request, CancellationToken ct)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, ct);
        if (product is null) return Result.Failure<bool>("Ürün bulunamadı.");

        var slug = request.Slug?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(slug))
        {
            var conflict = await _db.Products
                .AnyAsync(p => p.Slug == slug && p.Id != request.ProductId, ct);
            if (conflict) return Result.Failure<bool>("Bu slug başka bir ürün tarafından kullanılıyor.");
        }

        product.Slug               = string.IsNullOrEmpty(slug) ? null : slug;
        product.MetaTitleI18n      = request.MetaTitleI18n;
        product.MetaDescriptionI18n = request.MetaDescriptionI18n;
        product.MetaKeywordsI18n   = request.MetaKeywordsI18n;
        product.UpdatedAt          = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
