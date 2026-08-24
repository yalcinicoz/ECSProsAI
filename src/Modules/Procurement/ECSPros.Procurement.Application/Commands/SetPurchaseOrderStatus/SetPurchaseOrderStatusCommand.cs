using ECSPros.Procurement.Application.Services;
using ECSPros.Procurement.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Commands.SetPurchaseOrderStatus;

/// <summary>
/// Durum ELLE yönetilir (İ3/İ4 — kesin eşleşme yok): draft→ordered→receiving→closed; draft/ordered→cancelled.
/// receiving bilgi amaçlıdır; closed "bu satın almayla işimiz bitti" beyanıdır, hiçbir kontrol dayatmaz.
/// </summary>
public record SetPurchaseOrderStatusCommand(Guid Id, string Status) : IRequest<Result<bool>>;

public class SetPurchaseOrderStatusCommandHandler(IProcurementDbContext db)
    : IRequestHandler<SetPurchaseOrderStatusCommand, Result<bool>>
{
    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        ["draft"] = ["ordered", "cancelled"],
        ["ordered"] = ["receiving", "closed", "cancelled"],
        ["receiving"] = ["closed", "ordered"],
        ["closed"] = ["receiving"],     // yanlışlıkla kapatılan geri açılabilir
        ["cancelled"] = [],
    };

    public async Task<Result<bool>> Handle(SetPurchaseOrderStatusCommand request, CancellationToken ct)
    {
        var status = (request.Status ?? "").Trim().ToLowerInvariant();
        if (!PurchaseOrder.Statuses.Contains(status)) return Result.Failure<bool>("Geçersiz durum.");
        var po = await db.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == request.Id, ct);
        if (po is null) return Result.Failure<bool>("Satın alma bulunamadı.");
        if (!Allowed.TryGetValue(po.Status, out var next) || !next.Contains(status))
            return Result.Failure<bool>($"'{po.Status}' durumundan '{status}' durumuna geçilemez.");
        po.Status = status;
        po.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
