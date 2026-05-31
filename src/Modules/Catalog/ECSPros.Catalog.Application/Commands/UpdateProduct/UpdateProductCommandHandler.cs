using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.UpdateProduct;

public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, Result<bool>>
{
    private readonly ICatalogDbContext _context;

    public UpdateProductCommandHandler(ICatalogDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (product is null)
            return Result.Failure<bool>("Ürün bulunamadı.");

        var now = DateTime.UtcNow;

        if (product.BasePrice != request.BasePrice)
            _context.ProductPriceHistories.Add(new Domain.Entities.ProductPriceHistory
            {
                ProductId     = product.Id,
                PriceField    = "base_price",
                OldValue      = product.BasePrice,
                NewValue      = request.BasePrice,
                ChangedAt     = now,
                ChangedBy     = request.UpdatedBy,
                ChangedByName = request.UpdatedByName,
            });

        if (product.BaseCost != request.BaseCost)
            _context.ProductPriceHistories.Add(new Domain.Entities.ProductPriceHistory
            {
                ProductId     = product.Id,
                PriceField    = "base_cost",
                OldValue      = product.BaseCost,
                NewValue      = request.BaseCost,
                ChangedAt     = now,
                ChangedBy     = request.UpdatedBy,
                ChangedByName = request.UpdatedByName,
            });

        product.NameI18n             = request.NameI18n;
        product.ShortDescriptionI18n = request.ShortDescriptionI18n;
        product.DescriptionI18n      = request.DescriptionI18n;
        product.BasePrice            = request.BasePrice;
        product.BaseCost             = request.BaseCost;
        product.TaxRate              = request.TaxRate;
        product.IsActive             = request.IsActive;
        product.SupplierId           = request.SupplierId;
        product.SupplierProductCode  = request.SupplierProductCode;
        product.UpdatedBy = request.UpdatedBy;
        product.UpdatedAt = now;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
