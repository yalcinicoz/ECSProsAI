using ECSPros.Procurement.Application.Services;
using ECSPros.Procurement.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Commands.SetReceiptBatchPurchaseOrders;

/// <summary>
/// Parti ↔ SA gevşek bağı (İ3): link | unlink. Bağlanan SA 'ordered' durumundaysa bilgi amaçlı
/// 'receiving'e alınır (çözmede geri alınmaz — bağ bilgidir, kesin eşleşme değildir).
/// </summary>
public record SetReceiptBatchPurchaseOrdersCommand(Guid ReceiptBatchId, List<Guid> PurchaseOrderIds, string Action)
    : IRequest<Result<int>>;

public class SetReceiptBatchPurchaseOrdersCommandHandler(IProcurementDbContext db)
    : IRequestHandler<SetReceiptBatchPurchaseOrdersCommand, Result<int>>
{
    public async Task<Result<int>> Handle(SetReceiptBatchPurchaseOrdersCommand request, CancellationToken ct)
    {
        var action = (request.Action ?? "").Trim().ToLowerInvariant();
        if (action is not ("link" or "unlink")) return Result.Failure<int>("Geçersiz işlem (link|unlink).");
        var batch = await db.ReceiptBatches.Include(b => b.PurchaseOrders)
            .FirstOrDefaultAsync(b => b.Id == request.ReceiptBatchId, ct);
        if (batch is null) return Result.Failure<int>("Parti bulunamadı.");

        var ids = request.PurchaseOrderIds.Distinct().ToList();
        if (ids.Count == 0) return Result.Success(0);
        var pos = await db.PurchaseOrders.Where(p => ids.Contains(p.Id)).ToListAsync(ct);
        var n = 0;

        foreach (var poId in ids)
        {
            var existing = batch.PurchaseOrders.FirstOrDefault(x => x.PurchaseOrderId == poId);
            if (action == "link")
            {
                var po = pos.FirstOrDefault(p => p.Id == poId);
                if (po is null) return Result.Failure<int>("Satın alma bulunamadı.");
                if (po.SupplierId != batch.SupplierId) return Result.Failure<int>($"{po.Code} başka bir tedarikçiye ait — bu partiye bağlanamaz.");
                if (existing is not null) continue;
                db.ReceiptBatchPurchaseOrders.Add(new ReceiptBatchPurchaseOrder { ReceiptBatchId = batch.Id, PurchaseOrderId = poId });
                if (po.Status == "ordered") po.Status = "receiving";   // bilgi amaçlı
                n++;
            }
            else if (existing is not null)
            {
                existing.IsDeleted = true;
                existing.DeletedAt = DateTime.UtcNow;
                n++;
            }
        }
        batch.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(n);
    }
}
