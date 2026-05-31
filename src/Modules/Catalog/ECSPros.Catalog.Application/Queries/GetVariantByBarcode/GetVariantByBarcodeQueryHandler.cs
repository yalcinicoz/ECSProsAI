using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetVariantByBarcode;

public class GetVariantByBarcodeQueryHandler
    : IRequestHandler<GetVariantByBarcodeQuery, Result<VariantByBarcodeDto>>
{
    private readonly ICatalogDbContext _context;

    public GetVariantByBarcodeQueryHandler(ICatalogDbContext context)
    {
        _context = context;
    }

    public async Task<Result<VariantByBarcodeDto>> Handle(
        GetVariantByBarcodeQuery request, CancellationToken cancellationToken)
    {
        var variant = await _context.ProductVariants
            .Include(v => v.Product)
            .Where(v => v.Barcode == request.Barcode && !v.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (variant is null)
            return Result.Failure<VariantByBarcodeDto>($"Barkod bulunamadı: {request.Barcode}");

        return Result<VariantByBarcodeDto>.Success(new VariantByBarcodeDto(
            variant.ProductId,
            variant.Id,
            variant.Sku,
            variant.Product.Code,
            variant.Product.NameI18n));
    }
}
