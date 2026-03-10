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

        product.NameI18n = request.NameI18n;
        product.ShortDescriptionI18n = request.ShortDescriptionI18n;
        product.IsActive = request.IsActive;
        product.UpdatedBy = request.UpdatedBy;
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
