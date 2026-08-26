using ECSPros.Inventory.Application.Services;
using ECSPros.Inventory.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Commands.ReceiveToBin;

/// <summary>
/// T5 (tedarik yerleştirme): mal kabul stok girişi — verilen BİRİME (bin) ekler. Stok satırı
/// varyant+birim düzeyinde upsert edilir (Section/Warehouse denormalize), hareket `purchase` tipiyle
/// ToBinId + belge referansı (sorting_entry) taşır. Tedarik girişinin TEK stok kapısı (K3).
/// </summary>
public record ReceiveToBinCommand(
    Guid VariantId,
    Guid BinId,
    int Quantity,
    string? ReferenceType,
    Guid? ReferenceId,
    Guid? CreatedBy) : IRequest<Result<ReceiveToBinResult>>;

public record ReceiveToBinResult(Guid MovementId, Guid WarehouseId, Guid SectionId);

public class ReceiveToBinCommandHandler(IInventoryDbContext db)
    : IRequestHandler<ReceiveToBinCommand, Result<ReceiveToBinResult>>
{
    public async Task<Result<ReceiveToBinResult>> Handle(ReceiveToBinCommand request, CancellationToken ct)
    {
        if (request.Quantity <= 0) return Result.Failure<ReceiveToBinResult>("Adet 0'dan büyük olmalı.");

        var bin = await (from b in db.WarehouseBins
                         join sec in db.WarehouseSections on b.SectionId equals sec.Id
                         join w in db.Warehouses on sec.WarehouseId equals w.Id
                         where b.Id == request.BinId && !b.IsDeleted
                         select new { Bin = b, Section = sec, Warehouse = w }).FirstOrDefaultAsync(ct);
        if (bin is null) return Result.Failure<ReceiveToBinResult>("Birim (raf) bulunamadı.");
        if (!bin.Warehouse.IsActive) return Result.Failure<ReceiveToBinResult>("Birimin deposu pasif.");
        if (!bin.Bin.IsActive) return Result.Failure<ReceiveToBinResult>("Birim (raf) pasif.");

        var whId = bin.Warehouse.Id; var secId = bin.Section.Id; var bId = bin.Bin.Id;
        StockMovement movement = null!;
        // Faz 0 (StockTx): varyant kilidi — tedarik girişi diğer stok mutasyonlarıyla serileşir.
        await StockTx.RunAsync(db, new[] { request.VariantId }, async () =>
        {
        var stock = await db.Stocks.FirstOrDefaultAsync(s =>
            s.VariantId == request.VariantId && s.BinId == request.BinId, ct);
        if (stock is null)
        {
            stock = new Stock
            {
                VariantId = request.VariantId,
                WarehouseId = whId,
                SectionId = secId,
                BinId = bId,
                StockType = "physical",
                Quantity = 0,
            };
            db.Stocks.Add(stock);
        }
        stock.Quantity += request.Quantity;
        stock.UpdatedAt = DateTime.UtcNow;

        movement = new StockMovement
        {
            VariantId = request.VariantId,
            ToWarehouseId = whId,
            ToBinId = bId,
            MovementType = "purchase",
            Quantity = request.Quantity,
            ReferenceType = request.ReferenceType,
            ReferenceId = request.ReferenceId,
            CreatedBy = request.CreatedBy,
        };
        db.StockMovements.Add(movement);
        await db.SaveChangesAsync(ct);
        }, ct);
        return Result.Success(new ReceiveToBinResult(movement.Id, whId, secId));
    }
}
