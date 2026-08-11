using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.UpdateSupplierProductPrices;

/// <summary>
/// Partner P1a (2026-08-11): pazaryeri satıcısının fiyat güncellemesi — pricing.write scope,
/// onay KAPISIZ (K5 kararı: içerik onaylı, fiyat/stok anında). Ürün SupplierProductCode ile
/// (stok ucuyla aynı adresleme), kalemler SKU ile eşlenir. Varyant BasePrice güncellenir;
/// Product.BasePrice aktif varyantların en düşük fiyatına çekilir (kart fiyat fallback'i
/// tutarlı kalsın — onay komutunun kalıbı). Liste tarafında ~10 dk Redis TTL'i vardır.
/// </summary>
public record UpdateSupplierProductPricesCommand(
    Guid SupplierId,
    string SupplierProductCode,
    List<SupplierPriceItem> Items) : IRequest<Result<SupplierPriceResult>>;

public record SupplierPriceItem(string Sku, decimal Price);

public record SupplierPriceResult(string ProductCode, int Updated, List<SupplierPriceError> Errors)
{
    public bool HasErrors => Errors.Count > 0;
}
public record SupplierPriceError(string Field, string Code, string Message);

public class UpdateSupplierProductPricesCommandHandler
    : IRequestHandler<UpdateSupplierProductPricesCommand, Result<SupplierPriceResult>>
{
    private readonly ICatalogDbContext _db;
    public UpdateSupplierProductPricesCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<SupplierPriceResult>> Handle(UpdateSupplierProductPricesCommand request, CancellationToken ct)
    {
        var product = await _db.Products
            .Include(p => p.Variants.Where(v => !v.IsDeleted))
            .FirstOrDefaultAsync(p => p.SupplierId == request.SupplierId
                && p.SupplierProductCode == request.SupplierProductCode, ct);

        if (product is null)
            return Result.Failure<SupplierPriceResult>(
                $"'{request.SupplierProductCode}' kodlu ürününüz bulunamadı (henüz onaylanmamış olabilir).");

        // Tümü ya da hiçbiri: herhangi bir kalem hatalıysa hiçbir fiyat yazılmaz
        // (kısmi uygulama satıcı tarafında sessiz tutarsızlık üretir).
        var bySku = product.Variants.ToDictionary(v => v.Sku, v => v);
        var errors = new List<SupplierPriceError>();
        var uygulanacak = new List<(ProductVariant Varyant, decimal Fiyat)>();
        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Sku) || !bySku.TryGetValue(item.Sku, out var varyant))
            { errors.Add(new($"items.{item.Sku}", "unknown_sku", $"'{item.Sku}' bu ürüne ait bir SKU değil.")); continue; }
            if (item.Price <= 0)
            { errors.Add(new($"items.{item.Sku}", "invalid_price", "Fiyat 0'dan büyük olmalıdır.")); continue; }
            uygulanacak.Add((varyant, item.Price));
        }
        if (errors.Count > 0)
            return Result.Success(new SupplierPriceResult(product.Code, 0, errors));

        foreach (var (varyant, fiyat) in uygulanacak)
        {
            varyant.BasePrice = fiyat;
            varyant.UpdatedAt = DateTime.UtcNow;
        }

        // Kart fiyat fallback'i Product.BasePrice — aktif varyantların en düşüğü (onay kalıbı)
        var aktifFiyatlar = product.Variants
            .Where(v => v.IsActive && v.BasePrice > 0)
            .Select(v => v.BasePrice)
            .ToList();
        if (aktifFiyatlar.Count > 0)
            product.BasePrice = aktifFiyatlar.Min();
        product.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success(new SupplierPriceResult(product.Code, uygulanacak.Count, []));
    }
}
