using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.RenumberPackage;

/// <summary>Pakete seriden YENİ numara verir (F4): eski numara geçmişe yazılır ve
/// havuza geri dönmez. Kargo süreci başlamış (gönderili/etiketli) paket
/// yeniden numaralandırılamaz. Kargo kodu varsa geçersiz kalacağı için o da
/// temizlenir ve geçmişe yazılır (yeni kod ayrıca atanmalıdır).</summary>
public record RenumberPackageCommand(
    Guid PackageId,
    string Reason,
    Guid ChangedBy) : IRequest<Result<string>>;

public class RenumberPackageCommandHandler : IRequestHandler<RenumberPackageCommand, Result<string>>
{
    private readonly IFulfillmentDbContext _context;
    private readonly IPackageNumberService _packageNumbers;

    public RenumberPackageCommandHandler(
        IFulfillmentDbContext context,
        IPackageNumberService packageNumbers)
    {
        _context = context;
        _packageNumbers = packageNumbers;
    }

    public async Task<Result<string>> Handle(RenumberPackageCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result.Failure<string>("Yeniden numaralandırma için gerekçe zorunludur.");

        var package = await _context.Packages
            .FirstOrDefaultAsync(p => p.Id == request.PackageId, cancellationToken);
        if (package is null)
            return Result.Failure<string>("Paket bulunamadı.");

        if (package.ShipmentId is not null || package.LabelPrintedAt is not null)
            return Result.Failure<string>(
                $"'{package.PackageNumber}' paketinin kargo süreci başlamış; yeniden numaralandırmak için önce kargo iptali gerekir.");

        var eskiNumara = package.PackageNumber;
        var eskiKargoKodu = package.CargoIntegrationCode;
        var yeniNumara = await _packageNumbers.GenerateAsync(package.FirmPlatformId, cancellationToken);

        _context.PackageCodeHistories.Add(new PackageCodeHistory
        {
            PackageId = package.Id,
            OldPackageNumber = eskiNumara,
            OldCargoIntegrationCode = eskiKargoKodu,
            ChangeType = "renumber",
            Reason = request.Reason.Trim(),
            CreatedBy = request.ChangedBy
        });

        package.PackageNumber = yeniNumara;
        if (package.Barcode == eskiNumara)
            package.Barcode = yeniNumara;
        if (eskiKargoKodu is not null)
        {
            // Eski numaraya bağlı kargo kodu geçersizdir; yenisi ayrıca atanmalı
            package.CargoIntegrationCode = null;
            package.CargoIntegrationCodeSource = null;
        }
        package.UpdatedBy = request.ChangedBy;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(yeniNumara);
    }
}
