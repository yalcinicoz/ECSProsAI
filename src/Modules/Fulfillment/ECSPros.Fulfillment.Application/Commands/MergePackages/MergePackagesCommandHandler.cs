using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.MergePackages;

public class MergePackagesCommandHandler : IRequestHandler<MergePackagesCommand, Result<Guid>>
{
    private readonly IFulfillmentDbContext _context;
    private readonly IPackageNumberService _packageNumbers;

    public MergePackagesCommandHandler(
        IFulfillmentDbContext context,
        IPackageNumberService packageNumbers)
    {
        _context = context;
        _packageNumbers = packageNumbers;
    }

    public async Task<Result<Guid>> Handle(MergePackagesCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result.Failure<Guid>("Paket birleştirme için gerekçe zorunludur.");

        if (request.PackageIds.Distinct().Count() < 2)
            return Result.Failure<Guid>("Birleştirme için en az iki farklı paket seçilmelidir.");

        var packages = await _context.Packages
            .Include(p => p.Items)
            .Where(p => request.PackageIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        if (packages.Count != request.PackageIds.Distinct().Count())
            return Result.Failure<Guid>("Seçilen paketlerden bazıları bulunamadı.");

        if (packages.Select(p => p.OrderId).Distinct().Count() > 1)
            return Result.Failure<Guid>("Yalnızca aynı siparişin paketleri birleştirilebilir.");

        var kilitli = packages.FirstOrDefault(p =>
            p.ShipmentId is not null || p.LabelPrintedAt is not null || p.Status != "packed");
        if (kilitli is not null)
            return Result.Failure<Guid>(
                $"'{kilitli.PackageNumber}' paketi birleştirilemez: kargoya verilmiş, etiketi basılmış veya durumu uygun değil.");

        var ilk = packages.OrderBy(p => p.SequenceInOrder).First();
        var yeniNumara = await _packageNumbers.GenerateAsync(ilk.FirmPlatformId, cancellationToken);

        var hedef = new Package
        {
            OrderId = ilk.OrderId,
            FirmPlatformId = ilk.FirmPlatformId,
            PackageNumber = yeniNumara,
            SequenceInOrder = ilk.SequenceInOrder,
            // Farklı tedarikçiler birleşiyorsa paket karma olur (SupplierId null)
            SupplierId = packages.Select(p => p.SupplierId).Distinct().Count() == 1 ? ilk.SupplierId : null,
            Barcode = yeniNumara,
            Weight = packages.All(p => p.Weight.HasValue) ? packages.Sum(p => p.Weight) : null,
            Desi = packages.All(p => p.Desi.HasValue) ? packages.Sum(p => p.Desi) : null,
            Status = "packed",
            PackedAt = DateTime.UtcNow,
            PackedBy = request.MergedBy
        };

        foreach (var kaynak in packages)
        {
            foreach (var item in kaynak.Items.ToList())
            {
                hedef.Items.Add(new PackageItem
                {
                    OrderItemId = item.OrderItemId,
                    VariantId = item.VariantId,
                    Quantity = item.Quantity
                });
                item.IsDeleted = true;
                item.DeletedAt = DateTime.UtcNow;
                item.DeletedBy = request.MergedBy;
            }

            kaynak.Status = "merged";
            kaynak.UpdatedBy = request.MergedBy;

            _context.PackageCodeHistories.Add(new PackageCodeHistory
            {
                PackageId = kaynak.Id,
                OldPackageNumber = kaynak.PackageNumber,
                OldCargoIntegrationCode = kaynak.CargoIntegrationCode,
                ChangeType = "merge",
                Reason = request.Reason.Trim(),
                CreatedBy = request.MergedBy
            });
        }

        _context.Packages.Add(hedef);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(hedef.Id);
    }
}
