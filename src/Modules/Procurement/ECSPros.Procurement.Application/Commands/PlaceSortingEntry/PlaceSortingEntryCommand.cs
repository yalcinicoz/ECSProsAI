using ECSPros.Inventory.Application.Commands.ReceiveToBin;
using ECSPros.Inventory.Application.Services;
using ECSPros.Procurement.Application.Services;
using ECSPros.Procurement.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Commands.PlaceSortingEntry;

/// <summary>
/// T5 Yerleştirme: sayım kaydını BİRİME atar → stok girer (İ1: stok girişinin tek kaynağı sayımdır).
/// Kısmi yerleştirmede kayıt bölünür: yerleşen adet placed olur, kalan yeni bekleyen kayıtta kalır.
/// Partili kayıtta birim, partinin deposunda olmalıdır (operasyon kuralı); partisiz kayıt her birime konabilir.
/// Stok yazımı Inventory.ReceiveToBinCommand ile yapılır (movement: purchase, Ref=sorting_entry, ToBinId).
/// </summary>
public record PlaceSortingEntryCommand(Guid EntryId, Guid BinId, decimal? Quantity, Guid? PlacedBy)
    : IRequest<Result<PlaceResult>>;

public record PlaceResult(Guid PlacedEntryId, decimal PlacedQuantity, Guid? RemainingEntryId, decimal RemainingQuantity);

public class PlaceSortingEntryCommandHandler(IProcurementDbContext db, IInventoryDbContext invDb, IMediator mediator)
    : IRequestHandler<PlaceSortingEntryCommand, Result<PlaceResult>>
{
    public async Task<Result<PlaceResult>> Handle(PlaceSortingEntryCommand request, CancellationToken ct)
    {
        var entry = await db.SortingEntries.FirstOrDefaultAsync(e => e.Id == request.EntryId, ct);
        if (entry is null) return Result.Failure<PlaceResult>("Sayım kaydı bulunamadı.");
        if (entry.PutawayStatus == "placed") return Result.Failure<PlaceResult>("Kayıt zaten yerleştirilmiş.");

        var placeQty = request.Quantity ?? entry.Quantity;
        if (placeQty <= 0) return Result.Failure<PlaceResult>("Adet 0'dan büyük olmalı.");
        if (placeQty > entry.Quantity) return Result.Failure<PlaceResult>($"Yerleştirilecek adet sayımı aşamaz (sayım: {entry.Quantity}).");
        var qtyInt = (int)Math.Round(placeQty, MidpointRounding.AwayFromZero);
        if (qtyInt <= 0) return Result.Failure<PlaceResult>("Adet 0'dan büyük olmalı.");

        // Parti-depo kuralı STOK YAZIMINDAN ÖNCE: partili kayıt yalnız partinin deposundaki birime konur.
        if (entry.ReceiptBatchId.HasValue)
        {
            var binWh = await (from b in invDb.WarehouseBins
                               join sec in invDb.WarehouseSections on b.SectionId equals sec.Id
                               where b.Id == request.BinId
                               select (Guid?)sec.WarehouseId).FirstOrDefaultAsync(ct);
            if (binWh is null) return Result.Failure<PlaceResult>("Birim (raf) bulunamadı.");
            var batchWh = await db.ReceiptBatches.Where(b => b.Id == entry.ReceiptBatchId.Value)
                .Select(b => b.WarehouseId).FirstOrDefaultAsync(ct);
            if (batchWh != Guid.Empty && batchWh != binWh.Value)
                return Result.Failure<PlaceResult>("Birim, partinin teslim alındığı depoda değil.");
        }

        // Stok girişi (Inventory) — birim aktiflik/varlık doğrulaması orada
        var receive = await mediator.Send(new ReceiveToBinCommand(
            entry.VariantId, request.BinId, qtyInt, "sorting_entry", entry.Id, request.PlacedBy), ct);
        if (receive.IsFailure) return Result.Failure<PlaceResult>(receive.Error!);

        Guid? remainingId = null;
        decimal remaining = entry.Quantity - placeQty;
        if (remaining > 0)
        {
            var rest = new SortingEntry
            {
                ReceiptBatchId = entry.ReceiptBatchId,
                VariantId = entry.VariantId,
                Quantity = remaining,
                UnitCost = entry.UnitCost,
                CreatedBy = entry.CreatedBy,
                CreatedAt = entry.CreatedAt,
            };
            db.SortingEntries.Add(rest);
            remainingId = rest.Id;
        }

        entry.Quantity = placeQty;
        entry.PutawayStatus = "placed";
        entry.PlacedBinId = request.BinId;
        entry.PlacedAt = DateTime.UtcNow;
        entry.StockMovementId = receive.Value!.MovementId;
        entry.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(new PlaceResult(entry.Id, placeQty, remainingId, remaining));
    }
}
