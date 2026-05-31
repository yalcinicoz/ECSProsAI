using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.SetProductAttributes;

public record ProductAttributeItem(Guid AttributeTypeId, Guid? AttributeValueId, string? CustomValue);

public record SetProductAttributesCommand(
    Guid ProductId,
    List<ProductAttributeItem> Attributes
) : IRequest<Result<bool>>;

public class SetProductAttributesCommandHandler : IRequestHandler<SetProductAttributesCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public SetProductAttributesCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(SetProductAttributesCommand request, CancellationToken ct)
    {
        var productExists = await _db.Products.AnyAsync(p => p.Id == request.ProductId, ct);
        if (!productExists)
            return Result.Failure<bool>("Ürün bulunamadı.");

        var existing = await _db.ProductAttributes
            .Where(a => a.ProductId == request.ProductId)
            .ToListAsync(ct);

        foreach (var item in request.Attributes)
        {
            var attr = existing.FirstOrDefault(a => a.AttributeTypeId == item.AttributeTypeId);

            if (attr is null)
            {
                _db.ProductAttributes.Add(new ProductAttribute
                {
                    Id = Guid.NewGuid(),
                    ProductId = request.ProductId,
                    AttributeTypeId = item.AttributeTypeId,
                    AttributeValueId = item.AttributeValueId,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                attr.AttributeValueId = item.AttributeValueId;
                attr.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
