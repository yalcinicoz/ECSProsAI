using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.SetVariantBarcode;

public record SetVariantBarcodeCommand(Guid VariantId, string? Barcode) : IRequest<Result<bool>>;

public class SetVariantBarcodeCommandHandler : IRequestHandler<SetVariantBarcodeCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public SetVariantBarcodeCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(SetVariantBarcodeCommand request, CancellationToken ct)
    {
        var variant = await _db.ProductVariants
            .FirstOrDefaultAsync(v => v.Id == request.VariantId, ct);

        if (variant is null)
            return Result.Failure<bool>("Varyant bulunamadı.");

        var barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim();

        if (barcode is not null)
        {
            var duplicate = await _db.ProductVariants
                .AnyAsync(v => v.Barcode == barcode && v.Id != request.VariantId, ct);
            if (duplicate)
                return Result.Failure<bool>($"'{barcode}' barkodu başka bir varyanta atanmış.");
        }

        variant.Barcode = barcode;
        variant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
