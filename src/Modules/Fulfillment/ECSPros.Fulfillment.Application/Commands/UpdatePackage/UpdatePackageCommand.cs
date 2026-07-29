using ECSPros.Fulfillment.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.UpdatePackage;

/// <summary>Paketin fiziksel bilgilerini (ağırlık/boyut/desi/barkod) günceller.
/// Kimlik alanları (paket no, kargo kodu) buradan DEĞİŞTİRİLEMEZ — onlar
/// renumber / cargo-code akışlarından geçer ve geçmişe iz bırakır (F4).</summary>
public record UpdatePackageCommand(
    Guid PackageId,
    decimal? Weight,
    decimal? Width,
    decimal? Height,
    decimal? Length,
    decimal? Desi,
    string? Barcode,
    Guid UpdatedBy) : IRequest<Result<bool>>;

public class UpdatePackageCommandHandler : IRequestHandler<UpdatePackageCommand, Result<bool>>
{
    private readonly IFulfillmentDbContext _context;

    public UpdatePackageCommandHandler(IFulfillmentDbContext context) => _context = context;

    public async Task<Result<bool>> Handle(UpdatePackageCommand request, CancellationToken cancellationToken)
    {
        var package = await _context.Packages
            .FirstOrDefaultAsync(p => p.Id == request.PackageId, cancellationToken);
        if (package is null)
            return Result.Failure<bool>("Paket bulunamadı.");

        if (package.ShipmentId is not null)
            return Result.Failure<bool>(
                $"'{package.PackageNumber}' paketi kargoya verilmiş; güncellemek için önce kargo iptali gerekir.");

        decimal? desi = request.Desi;
        if (!desi.HasValue && request.Width.HasValue && request.Height.HasValue && request.Length.HasValue)
            desi = request.Width.Value * request.Height.Value * request.Length.Value / 3000m;

        package.Weight = request.Weight ?? package.Weight;
        package.Width = request.Width ?? package.Width;
        package.Height = request.Height ?? package.Height;
        package.Length = request.Length ?? package.Length;
        package.Desi = desi ?? package.Desi;
        if (!string.IsNullOrWhiteSpace(request.Barcode))
            package.Barcode = request.Barcode!.Trim();
        package.UpdatedBy = request.UpdatedBy;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
