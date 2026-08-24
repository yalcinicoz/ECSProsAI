using ECSPros.Catalog.Application.Services;
using ECSPros.Procurement.Application.Services;
using ECSPros.Procurement.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Commands.AccumulateSortingCount;

/// <summary>
/// T4 revizyonu: GERÇEK SAYIM = depoya teslim okutması. Okutma modu delta=1, adet modu delta=N;
/// aynı (parti, varyant) için BEKLEYEN kayıtta birikir (yerleşmiş kayda dokunulmaz — yeni bekleyen açılır).
/// Sonuç: kaydın Id'si + birikmiş toplam.
/// </summary>
public record AccumulateSortingCountCommand(
    Guid? ReceiptBatchId,
    Guid VariantId,
    decimal QuantityDelta,
    decimal? UnitCost,
    Guid? CreatedBy) : IRequest<Result<AccumulateResult>>;

public record AccumulateResult(Guid EntryId, decimal Quantity);

public class AccumulateSortingCountCommandHandler(IProcurementDbContext db, ICatalogDbContext catDb)
    : IRequestHandler<AccumulateSortingCountCommand, Result<AccumulateResult>>
{
    public async Task<Result<AccumulateResult>> Handle(AccumulateSortingCountCommand request, CancellationToken ct)
    {
        if (request.QuantityDelta <= 0) return Result.Failure<AccumulateResult>("Adet 0'dan büyük olmalı.");
        if (request.UnitCost is < 0) return Result.Failure<AccumulateResult>("Maliyet negatif olamaz.");

        var variantExists = await catDb.ProductVariants.AsNoTracking().AnyAsync(v => v.Id == request.VariantId, ct);
        if (!variantExists) return Result.Failure<AccumulateResult>("Varyant bulunamadı — kart eksikse bildirim düşün (K9).");

        if (request.ReceiptBatchId.HasValue)
        {
            var batch = await db.ReceiptBatches.FirstOrDefaultAsync(b => b.Id == request.ReceiptBatchId.Value, ct);
            if (batch is null) return Result.Failure<AccumulateResult>("Parti bulunamadı.");
            if (batch.Status == "completed") return Result.Failure<AccumulateResult>("Tamamlanmış partiye sayım eklenemez (önce Geri Aç).");
            if (batch.Status == "received") batch.Status = "sorting";
        }

        var entry = await db.SortingEntries.FirstOrDefaultAsync(e =>
            e.ReceiptBatchId == request.ReceiptBatchId && e.VariantId == request.VariantId
            && e.PutawayStatus == "pending", ct);
        if (entry is null)
        {
            entry = new SortingEntry
            {
                ReceiptBatchId = request.ReceiptBatchId,
                VariantId = request.VariantId,
                Quantity = 0,
                CreatedBy = request.CreatedBy,
            };
            db.SortingEntries.Add(entry);
        }
        entry.Quantity += request.QuantityDelta;
        if (request.UnitCost.HasValue) entry.UnitCost = request.UnitCost;
        entry.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(new AccumulateResult(entry.Id, entry.Quantity));
    }
}
