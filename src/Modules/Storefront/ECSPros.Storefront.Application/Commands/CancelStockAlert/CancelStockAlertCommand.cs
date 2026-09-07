using ECSPros.Storefront.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.CancelStockAlert;

/// <summary>B3 (2026-09-07, mobil): üyenin stok alarmından vazgeçmesi — kayıt "cancelled" olur
/// (bildirici kancası cancelled kayıtları atlar). Yalnız sahibi iptal edebilir; alertId ya da
/// (firmPlatformId + variantId) ile adreslenir.</summary>
public record CancelStockAlertCommand(Guid MemberId, Guid? AlertId = null, Guid? FirmPlatformId = null, Guid? VariantId = null)
    : IRequest<Result<bool>>;

public class CancelStockAlertCommandHandler(IStorefrontDbContext db) : IRequestHandler<CancelStockAlertCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(CancelStockAlertCommand request, CancellationToken ct)
    {
        var q = db.StockAlerts.Where(a => a.MemberId == request.MemberId && a.Status == "active");
        if (request.AlertId is { } id) q = q.Where(a => a.Id == id);
        else if (request.VariantId is { } vid)
        {
            q = q.Where(a => a.VariantId == vid);
            if (request.FirmPlatformId is { } fp) q = q.Where(a => a.FirmPlatformId == fp);
        }
        else return Result.Failure<bool>("alertId veya variantId gerekli.");

        var kayitlar = await q.ToListAsync(ct);
        if (kayitlar.Count == 0) return Result.Failure<bool>("Stok alarmı bulunamadı.");
        foreach (var a in kayitlar) { a.Status = "cancelled"; a.UpdatedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
