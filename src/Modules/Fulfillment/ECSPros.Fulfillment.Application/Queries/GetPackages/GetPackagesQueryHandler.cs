using ECSPros.Fulfillment.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Queries.GetPackages;

public class GetPackagesQueryHandler : IRequestHandler<GetPackagesQuery, Result<List<PackageDto>>>
{
    private readonly IFulfillmentDbContext _context;

    public GetPackagesQueryHandler(IFulfillmentDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<PackageDto>>> Handle(GetPackagesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Packages.AsQueryable();

        if (request.OrderId.HasValue)
            query = query.Where(p => p.OrderId == request.OrderId);

        var items = await query
            .Select(p => new PackageDto(
                p.Id,
                p.OrderId,
                p.ShipmentId,
                p.PackageNumber,
                p.Barcode,
                p.Weight,
                p.Desi,
                p.Status,
                p.PackedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(items);
    }
}
