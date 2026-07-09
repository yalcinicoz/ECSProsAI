using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.CreateStockAlert;

/// <summary>
/// C9: "Stok gelince haber ver" kaydı. İdempotent — üyenin aynı varyant için aktif
/// kaydı varsa yenisi açılmaz, mevcut kayıt AlreadyExists=true ile döner.
/// </summary>
public record CreateStockAlertCommand(
    Guid FirmPlatformId,
    Guid VariantId,
    Guid MemberId,
    string? Email,
    string? ProductCode = null,
    string? VariantInfo = null) : IRequest<Result<CreateStockAlertResult>>;

public record CreateStockAlertResult(Guid AlertId, bool AlreadyExists);

public class CreateStockAlertCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<CreateStockAlertCommand, Result<CreateStockAlertResult>>
{
    public async Task<Result<CreateStockAlertResult>> Handle(CreateStockAlertCommand request, CancellationToken ct)
    {
        if (request.VariantId == Guid.Empty)
            return Result.Failure<CreateStockAlertResult>("Varyant belirtilmedi.");

        var mevcut = await db.StockAlerts.FirstOrDefaultAsync(a =>
            a.FirmPlatformId == request.FirmPlatformId
            && a.VariantId == request.VariantId
            && a.MemberId == request.MemberId
            && a.Status == "active", ct);
        if (mevcut is not null)
            return Result.Success(new CreateStockAlertResult(mevcut.Id, AlreadyExists: true));

        var kayit = new StockAlert
        {
            FirmPlatformId = request.FirmPlatformId,
            VariantId = request.VariantId,
            MemberId = request.MemberId,
            Email = request.Email,
            ProductCode = request.ProductCode,
            VariantInfo = request.VariantInfo
        };
        db.StockAlerts.Add(kayit);
        await db.SaveChangesAsync(ct);

        return Result.Success(new CreateStockAlertResult(kayit.Id, AlreadyExists: false));
    }
}
