using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Fulfillment.Application.Commands.CreatePackage;

public class CreatePackageCommandHandler : IRequestHandler<CreatePackageCommand, Result<Guid>>
{
    private readonly IFulfillmentDbContext _context;

    public CreatePackageCommandHandler(IFulfillmentDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreatePackageCommand request, CancellationToken cancellationToken)
    {
        decimal? desi = request.Desi;
        if (!desi.HasValue && request.Width.HasValue && request.Height.HasValue && request.Length.HasValue)
            desi = request.Width.Value * request.Height.Value * request.Length.Value / 3000m;

        var package = new Package
        {
            OrderId = request.OrderId,
            ShipmentId = request.ShipmentId,
            PackageNumber = request.PackageNumber,
            Barcode = request.Barcode,
            Weight = request.Weight,
            Width = request.Width,
            Height = request.Height,
            Length = request.Length,
            Desi = desi,
            Status = "packed",
            PackedAt = DateTime.UtcNow,
            PackedBy = request.PackedBy
        };

        _context.Packages.Add(package);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(package.Id);
    }
}
