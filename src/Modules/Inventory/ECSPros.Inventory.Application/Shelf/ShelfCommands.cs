using ECSPros.Inventory.Application.Services;
using ECSPros.Inventory.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Shelf;

// ─── R2 Rafa Yerleştir ────────────────────────────────────────────────────────

public static class PlaceSources { public const string Free = "free", Unbinned = "unbinned"; }

/// <summary>Göze ürün koyar. <c>free</c>: sayım dışı serbest giriş (<c>adjustment</c>, not zorunlu);
/// <c>unbinned</c>: depodaki rafsız (BinId null) eski-tarz satırdan taşır (<c>transfer</c>).
/// Mal kabul kolisinden yerleştirme Tedarik › Sayım/Teslim akışındadır (ReceiveToBin).</summary>
public record PlaceToBinCommand(Guid BinId, Guid VariantId, int Quantity, string Source, string? Notes, Guid? CreatedBy)
    : IRequest<Result<int>>;

public class PlaceToBinCommandHandler(IInventoryDbContext db) : IRequestHandler<PlaceToBinCommand, Result<int>>
{
    public async Task<Result<int>> Handle(PlaceToBinCommand request, CancellationToken ct)
    {
        if (request.Quantity <= 0) return Result.Failure<int>("Adet 0'dan büyük olmalı.");
        if (request.Source == PlaceSources.Free && string.IsNullOrWhiteSpace(request.Notes))
            return Result.Failure<int>("Serbest girişte not zorunludur (nereden geldiği).");

        var bin = await ShelfOps.FindBinAsync(db, request.BinId, null, ct);
        if (bin is null) return Result.Failure<int>("Göz bulunamadı.");
        if (ShelfOps.YazilabilirMi(bin) is { } hata) return Result.Failure<int>(hata);

        string? err = null; var yeni = 0;
        await StockTx.RunAsync(db, new[] { request.VariantId }, async () =>
        {
            var b = (await ShelfOps.FindBinAsync(db, request.BinId, null, ct))!;
            var dst = await ShelfOps.GetOrCreateAsync(db, request.VariantId, b, ct);
            if (request.Source == PlaceSources.Unbinned)
            {
                var ciplak = await db.Stocks.FirstOrDefaultAsync(s => s.VariantId == request.VariantId && s.WarehouseId == b.Warehouse.Id && s.BinId == null && s.Quantity > 0, ct);
                var serbest = ciplak is null ? 0 : ciplak.Quantity - ciplak.ReservedQuantity;
                if (ciplak is null || serbest < request.Quantity) { err = $"Depoda rafsız serbest adet {serbest}; {request.Quantity} taşınamaz."; return; }
                ciplak.Quantity -= request.Quantity; ciplak.UpdatedAt = DateTime.UtcNow;
                ShelfOps.BosSatiriKaldir(db, ciplak);
                db.StockMovements.Add(ShelfOps.Hareket(request.VariantId, StokHareketTipi.Transfer, request.Quantity,
                    b.Warehouse.Id, null, b.Warehouse.Id, b.Bin.Id, "shelf", null, request.Notes ?? "Rafsız satırdan göze", request.CreatedBy));
            }
            else
            {
                db.StockMovements.Add(ShelfOps.Hareket(request.VariantId, StokHareketTipi.Adjustment, request.Quantity,
                    null, null, b.Warehouse.Id, b.Bin.Id, "shelf", null, request.Notes, request.CreatedBy));
            }
            dst.Quantity += request.Quantity; dst.UpdatedAt = DateTime.UtcNow;
            yeni = dst.Quantity;
            await db.SaveChangesAsync(ct);
        }, ct);
        return err is null ? Result.Success(yeni) : Result.Failure<int>(err);
    }
}

// ─── R3 Raftan Rafa ───────────────────────────────────────────────────────────

public record MoveItem(Guid VariantId, int? Quantity);   // Quantity null → o ürünün satırı bütünüyle

/// <summary>Aynı depoda göz→göz taşıma. <paramref name="MoveAll"/> → kaynak gözün tüm satırları rezervasyonlarıyla
/// hedefe geçer; değilse <paramref name="Items"/> (adetli → yalnız serbest adet, adetsiz → satır bütünüyle).</summary>
public record MoveBetweenBinsCommand(Guid FromBinId, Guid ToBinId, bool MoveAll, List<MoveItem>? Items, string? Notes, Guid? CreatedBy)
    : IRequest<Result<int>>;

public class MoveBetweenBinsCommandHandler(IInventoryDbContext db) : IRequestHandler<MoveBetweenBinsCommand, Result<int>>
{
    public async Task<Result<int>> Handle(MoveBetweenBinsCommand request, CancellationToken ct)
    {
        var from = await ShelfOps.FindBinAsync(db, request.FromBinId, null, ct);
        var to = await ShelfOps.FindBinAsync(db, request.ToBinId, null, ct);
        if (from is null || to is null) return Result.Failure<int>("Kaynak ya da hedef göz bulunamadı.");
        if (ShelfOps.YazilabilirMi(to) is { } hata) return Result.Failure<int>("Hedef: " + hata);
        if (from.Warehouse.Id != to.Warehouse.Id) return Result.Failure<int>("Göz→göz taşıma yalnız aynı depo içinde; depolar arası için mağaza taşıma / transfer kullanın.");
        if (from.Bin.Id == to.Bin.Id) return Result.Failure<int>("Kaynak ve hedef göz aynı.");

        List<MoveItem> items;
        if (request.MoveAll)
            items = await db.Stocks.AsNoTracking().Where(s => s.BinId == from.Bin.Id && (s.Quantity > 0 || s.ReservedQuantity > 0))
                .Select(s => new MoveItem(s.VariantId, null)).ToListAsync(ct);
        else items = request.Items ?? [];
        if (items.Count == 0) return Result.Failure<int>("Taşınacak ürün yok.");

        string? err = null; var toplam = 0;
        await StockTx.RunAsync(db, items.Select(i => i.VariantId), async () =>
        {
            var f = (await ShelfOps.FindBinAsync(db, request.FromBinId, null, ct))!;
            var t = (await ShelfOps.FindBinAsync(db, request.ToBinId, null, ct))!;
            foreach (var it in items)
            {
                var (moved, e) = await ShelfOps.MoveAsync(db, it.VariantId, f, t, it.Quantity, StokHareketTipi.Transfer, "shelf", null, request.Notes, request.CreatedBy, ct);
                if (e is not null) { err = e; return; }   // transaction geri alınır (RunAsync commit etmez → exception değil; aşağıda)
                toplam += moved;
            }
            await db.SaveChangesAsync(ct);
        }, ct);
        return err is null ? Result.Success(toplam) : Result.Failure<int>(err);
    }
}

// ─── R4 İadeden Rafa ──────────────────────────────────────────────────────────

/// <summary>Satışa KAPALI kısımdaki gözden (iade/defo girişi) satış rafına ya da Defo kısmına taşır.</summary>
public record ReturnToShelfCommand(Guid FromBinId, Guid ToBinId, Guid VariantId, int Quantity, Guid? ReturnId, string? Notes, Guid? CreatedBy)
    : IRequest<Result<int>>;

public class ReturnToShelfCommandHandler(IInventoryDbContext db) : IRequestHandler<ReturnToShelfCommand, Result<int>>
{
    public async Task<Result<int>> Handle(ReturnToShelfCommand request, CancellationToken ct)
    {
        if (request.Quantity <= 0) return Result.Failure<int>("Adet 0'dan büyük olmalı.");
        var from = await ShelfOps.FindBinAsync(db, request.FromBinId, null, ct);
        var to = await ShelfOps.FindBinAsync(db, request.ToBinId, null, ct);
        if (from is null || to is null) return Result.Failure<int>("Kaynak ya da hedef göz bulunamadı.");
        if (from.Section.IsSellableOnline) return Result.Failure<int>("Kaynak göz satışa açık bir kısımda; iadeden rafa yalnız kapalı kısımdan (İade/Defo) yapılır. Raftan rafa taşımayı kullanın.");
        if (ShelfOps.YazilabilirMi(to) is { } hata) return Result.Failure<int>("Hedef: " + hata);

        string? err = null; var moved = 0;
        await StockTx.RunAsync(db, new[] { request.VariantId }, async () =>
        {
            var f = (await ShelfOps.FindBinAsync(db, request.FromBinId, null, ct))!;
            var t = (await ShelfOps.FindBinAsync(db, request.ToBinId, null, ct))!;
            var (m, e) = await ShelfOps.MoveAsync(db, request.VariantId, f, t, request.Quantity, StokHareketTipi.ReturnToShelf,
                request.ReturnId.HasValue ? "return" : "shelf", request.ReturnId, request.Notes, request.CreatedBy, ct);
            if (e is not null) { err = e; return; }
            moved = m;
            await db.SaveChangesAsync(ct);
        }, ct);
        return err is null ? Result.Success(moved) : Result.Failure<int>(err);
    }
}

// ─── R6 Mağaza Reyon ──────────────────────────────────────────────────────────

/// <summary>Depolar arası ANLIK tek ürün taşıma (mağaza reyonu ↔ merkez): kaynaktan tüketilir, hedefin uygun gözüne
/// girer; iz için tamamlanmış bir transfer kaydı açılır. Rezervli adet taşınmaz.</summary>
public record StoreMoveCommand(Guid FromWarehouseId, Guid ToWarehouseId, Guid VariantId, int Quantity, string? Notes, Guid CreatedBy)
    : IRequest<Result<Guid>>;

public class StoreMoveCommandHandler(IInventoryDbContext db) : IRequestHandler<StoreMoveCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(StoreMoveCommand request, CancellationToken ct)
    {
        if (request.Quantity <= 0) return Result.Failure<Guid>("Adet 0'dan büyük olmalı.");
        if (request.FromWarehouseId == request.ToWarehouseId) return Result.Failure<Guid>("Kaynak ve hedef depo aynı.");
        var from = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == request.FromWarehouseId, ct);
        var to = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == request.ToWarehouseId, ct);
        if (from is null || to is null) return Result.Failure<Guid>("Depo bulunamadı.");
        if (!from.IsActive || !to.IsActive) return Result.Failure<Guid>("Pasif depoyla taşıma yapılamaz.");

        string? err = null; Guid transferId = Guid.Empty;
        await StockTx.RunAsync(db, new[] { request.VariantId }, async () =>
        {
            var serbest = await db.Stocks.Where(s => s.VariantId == request.VariantId && s.WarehouseId == from.Id)
                .SumAsync(s => (int?)(s.Quantity - s.ReservedQuantity), ct) ?? 0;
            if (serbest < request.Quantity) { err = $"'{from.Code}' deposunda serbest adet {serbest}; {request.Quantity} taşınamaz."; return; }

            await StockOps.ConsumeAsync(db, request.VariantId, from.Id, request.Quantity, ct);
            await StockOps.ReceiveAsync(db, request.VariantId, to.Id, request.Quantity, preferReturns: false, ct);

            var sayi = await db.TransferRequests.CountAsync(t => t.CreatedAt >= DateTime.UtcNow.Date, ct);
            var tr = new TransferRequest
            {
                Code = $"TR-{DateTime.UtcNow:yyyyMMdd}-{sayi + 1:D4}-M", FromWarehouseId = from.Id, ToWarehouseId = to.Id,
                TransferType = StokHareketTipi.StoreMove, Status = "completed", RequestedBy = request.CreatedBy,
                Notes = request.Notes ?? "Mağaza reyon taşıma (anlık)",
            };
            tr.Items.Add(new TransferRequestItem
            {
                VariantId = request.VariantId, RequestedQuantity = request.Quantity, PickedQuantity = request.Quantity,
                DeliveredQuantity = request.Quantity, Status = "completed",
            });
            db.TransferRequests.Add(tr);
            transferId = tr.Id;
            db.StockMovements.Add(ShelfOps.Hareket(request.VariantId, StokHareketTipi.StoreMove, request.Quantity,
                from.Id, null, to.Id, null, "transfer", tr.Id, request.Notes, request.CreatedBy));
            await db.SaveChangesAsync(ct);
        }, ct);
        return err is null ? Result.Success(transferId) : Result.Failure<Guid>(err);
    }
}
