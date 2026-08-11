using ECSPros.Fulfillment.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Queries.GetSupplierPackages;

/// <summary>
/// Partner P1b (2026-08-11): verilen siparişlerdeki, SATICIYA ait paketler — sipariş
/// görünümüne iliştirilir (paket operasyonda oluşur; henüz paketlenmemiş siparişte boş liste
/// normaldir). Karma (SupplierId null) paketler satıcıya GÖSTERİLMEZ.
/// </summary>
public record GetSupplierPackagesQuery(Guid SupplierId, List<Guid> OrderIds)
    : IRequest<Result<List<SupplierPackageDto>>>;

public record SupplierPackageDto(
    Guid OrderId,
    string PackageNumber,
    string Status,
    DateTime? PackedAt,
    List<SupplierPackageItemDto> Items);

public record SupplierPackageItemDto(Guid OrderItemId, int Quantity);

public class GetSupplierPackagesQueryHandler
    : IRequestHandler<GetSupplierPackagesQuery, Result<List<SupplierPackageDto>>>
{
    private readonly IFulfillmentDbContext _db;
    public GetSupplierPackagesQueryHandler(IFulfillmentDbContext db) => _db = db;

    public async Task<Result<List<SupplierPackageDto>>> Handle(GetSupplierPackagesQuery request, CancellationToken ct)
    {
        if (request.OrderIds.Count == 0)
            return Result.Success(new List<SupplierPackageDto>());

        var paketler = await _db.Packages.AsNoTracking()
            .Where(p => p.SupplierId == request.SupplierId && request.OrderIds.Contains(p.OrderId))
            .Include(p => p.Items)
            .OrderBy(p => p.SequenceInOrder)
            .ToListAsync(ct);

        return Result.Success(paketler.Select(p => new SupplierPackageDto(
            p.OrderId, p.PackageNumber, p.Status, p.PackedAt,
            p.Items.Select(i => new SupplierPackageItemDto(i.OrderItemId, i.Quantity)).ToList()
        )).ToList());
    }
}
