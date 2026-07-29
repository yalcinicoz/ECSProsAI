using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Domain.Entities;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.AssignCargoCode;

public class AssignCargoCodeCommandHandler : IRequestHandler<AssignCargoCodeCommand, Result<string>>
{
    private readonly IFulfillmentDbContext _context;
    private readonly ICargoCodeService _cargoCodes;

    public AssignCargoCodeCommandHandler(IFulfillmentDbContext context, ICargoCodeService cargoCodes)
    {
        _context = context;
        _cargoCodes = cargoCodes;
    }

    public async Task<Result<string>> Handle(AssignCargoCodeCommand request, CancellationToken cancellationToken)
    {
        var package = await _context.Packages
            .FirstOrDefaultAsync(p => p.Id == request.PackageId, cancellationToken);
        if (package is null)
            return Result.Failure<string>("Paket bulunamadı.");

        // Kargo süreci başlamış paketin kodu değiştirilemez (önce kargo iptali gerekir)
        if (package.ShipmentId is not null || package.LabelPrintedAt is not null)
            return Result.Failure<string>(
                $"'{package.PackageNumber}' paketinin kargo süreci başlamış; kod değiştirmek için önce kargo iptali gerekir.");

        string kod;
        string kaynak;
        if (!string.IsNullOrWhiteSpace(request.ExternalCode))
        {
            kod = request.ExternalCode!.Trim();
            kaynak = "external";
        }
        else
        {
            if (request.FirmPlatformIntegrationId is not { } entegrasyonId)
                return Result.Failure<string>("Kargo entegrasyonu seçilmedi (veya dış kod girilmedi).");

            var sonuc = await _cargoCodes.GenerateAsync(entegrasyonId, package.PackageNumber, cancellationToken);
            if (!sonuc.IsSuccess)
                return Result.Failure<string>(sonuc.Error!);
            kod = sonuc.Code!;
            kaynak = "generated";
        }

        // Eski kod izi — kodlar havuza geri dönmez, geçmişte saklanır (karar 2026-07-19)
        if (!string.IsNullOrEmpty(package.CargoIntegrationCode) && package.CargoIntegrationCode != kod)
        {
            _context.PackageCodeHistories.Add(new PackageCodeHistory
            {
                PackageId = package.Id,
                OldCargoIntegrationCode = package.CargoIntegrationCode,
                ChangeType = "cargo_change",
                Reason = string.IsNullOrWhiteSpace(request.Reason)
                    ? "Kargo kodu yeniden atandı."
                    : request.Reason!.Trim(),
                CreatedBy = request.ChangedBy
            });
        }

        package.CargoIntegrationCode = kod;
        package.CargoIntegrationCodeSource = kaynak;
        package.UpdatedBy = request.ChangedBy;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(kod);
    }
}
