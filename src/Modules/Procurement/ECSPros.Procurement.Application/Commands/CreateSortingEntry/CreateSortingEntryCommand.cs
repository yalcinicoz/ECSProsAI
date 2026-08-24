using ECSPros.Catalog.Application.Services;
using ECSPros.Procurement.Application.Services;
using ECSPros.Procurement.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Commands.CreateSortingEntry;

/// <summary>
/// Sayım kaydı (İ1): varyant MEVCUT olmalı (K9 — kart açılmaz). Partili kayıtta 'received' parti
/// kendiliğinden 'sorting'e alınır; 'completed' partiye kayıt yapılamaz (Geri Aç gerekir).
/// </summary>
public record CreateSortingEntryCommand(
    Guid? ReceiptBatchId,
    Guid VariantId,
    decimal Quantity,
    decimal? UnitCost,
    Guid? CreatedBy) : IRequest<Result<Guid>>;

public class CreateSortingEntryCommandHandler(IProcurementDbContext db, ICatalogDbContext catDb)
    : IRequestHandler<CreateSortingEntryCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateSortingEntryCommand request, CancellationToken ct)
    {
        if (request.Quantity <= 0) return Result.Failure<Guid>("Adet 0'dan büyük olmalı.");
        if (request.UnitCost is < 0) return Result.Failure<Guid>("Maliyet negatif olamaz.");

        var variantExists = await catDb.ProductVariants.AsNoTracking().AnyAsync(v => v.Id == request.VariantId, ct);
        if (!variantExists) return Result.Failure<Guid>("Varyant bulunamadı — kart eksikse bildirim düşün (K9).");

        if (request.ReceiptBatchId.HasValue)
        {
            var batch = await db.ReceiptBatches.FirstOrDefaultAsync(b => b.Id == request.ReceiptBatchId.Value, ct);
            if (batch is null) return Result.Failure<Guid>("Parti bulunamadı.");
            if (batch.Status == "completed") return Result.Failure<Guid>("Tamamlanmış partiye sayım eklenemez (önce Geri Aç).");
            if (batch.Status == "received") batch.Status = "sorting";
        }

        var entry = new SortingEntry
        {
            ReceiptBatchId = request.ReceiptBatchId,
            VariantId = request.VariantId,
            Quantity = request.Quantity,
            UnitCost = request.UnitCost,
            CreatedBy = request.CreatedBy,
        };
        db.SortingEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return Result.Success(entry.Id);
    }
}
