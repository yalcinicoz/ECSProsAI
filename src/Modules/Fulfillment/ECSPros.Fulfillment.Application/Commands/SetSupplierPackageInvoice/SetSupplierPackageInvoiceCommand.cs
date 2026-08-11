using ECSPros.Fulfillment.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.SetSupplierPackageInvoice;

/// <summary>
/// Satıcı paneli (2026-08-11): satıcının kendi kestiği fatura bilgisini PAKETİNE yazar
/// (paket başına fatura kuralı). Owner-scope: paket satıcının olmalı. Tekrar çağrı günceller.
/// </summary>
public record SetSupplierPackageInvoiceCommand(
    Guid SupplierId,
    Guid PackageId,
    string InvoiceNumber,
    string? InvoiceUrl) : IRequest<Result<bool>>;

public class SetSupplierPackageInvoiceCommandHandler(IFulfillmentDbContext context)
    : IRequestHandler<SetSupplierPackageInvoiceCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(SetSupplierPackageInvoiceCommand request, CancellationToken ct)
    {
        var package = await context.Packages
            .FirstOrDefaultAsync(p => p.Id == request.PackageId && p.SupplierId == request.SupplierId, ct);
        if (package is null) return Result.Failure<bool>("Size ait böyle bir paket bulunamadı.");

        package.SupplierInvoiceNumber = request.InvoiceNumber;
        package.SupplierInvoiceUrl = request.InvoiceUrl;
        package.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
