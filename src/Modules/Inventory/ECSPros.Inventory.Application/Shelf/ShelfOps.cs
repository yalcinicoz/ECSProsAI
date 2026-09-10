using ECSPros.Inventory.Application.Services;
using ECSPros.Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Shelf;

/// <summary>
/// Raf/göz operasyonlarının ortak çekirdeği (FAZ 15.3, docs/raf-operasyon-ekranlari-plani.md §3).
/// Kurallar: her mutasyon çağıranın açtığı <see cref="StockTx"/> kilidi altında; 0'lı satır bırakılmaz (K-14);
/// pasif göz/kısım/depoya yazılmaz; göz→göz taşıma yalnız aynı depo içinde; rezervasyon yalnız satır bütünüyle
/// taşınır (K2), kısmi taşıma serbest (Quantity − Reserved) adetle sınırlıdır.
/// </summary>
public static class ShelfOps
{
    public sealed record BinInfo(WarehouseBin Bin, WarehouseSection Section, Warehouse Warehouse);

    /// <summary>Gözü Id ya da barkodla bulur (silinmişler hariç; pasifler döner — çağıran karar verir).</summary>
    public static async Task<BinInfo?> FindBinAsync(IInventoryDbContext db, Guid? binId, string? barcode, CancellationToken ct)
    {
        var q = from b in db.WarehouseBins
                join sec in db.WarehouseSections on b.SectionId equals sec.Id
                join w in db.Warehouses on sec.WarehouseId equals w.Id
                select new { b, sec, w };
        if (binId.HasValue) q = q.Where(x => x.b.Id == binId.Value);
        else if (!string.IsNullOrWhiteSpace(barcode)) { var bc = barcode.Trim(); q = q.Where(x => x.b.Barcode == bc); }
        else return null;
        var r = await q.FirstOrDefaultAsync(ct);
        return r is null ? null : new BinInfo(r.b, r.sec, r.w);
    }

    /// <summary>Yazma ön koşulu: depo, kısım ve göz aktif olmalı.</summary>
    public static string? YazilabilirMi(BinInfo bin)
    {
        if (!bin.Warehouse.IsActive) return $"'{bin.Warehouse.Code}' deposu pasif.";
        if (!bin.Section.IsActive) return $"'{bin.Section.Code}' kısmı pasif.";
        if (!bin.Bin.IsActive) return $"'{bin.Bin.Code}' gözü pasif.";
        return null;
    }

    /// <summary>Gözdeki varyant satırı (tracked); yoksa 0'lı satır açar (kaydetmez — çağıran SaveChanges).</summary>
    public static async Task<Stock> GetOrCreateAsync(IInventoryDbContext db, Guid variantId, BinInfo bin, CancellationToken ct)
    {
        var st = await db.Stocks.FirstOrDefaultAsync(s => s.VariantId == variantId && s.BinId == bin.Bin.Id, ct);
        if (st is not null) return st;
        st = new Stock
        {
            VariantId = variantId, WarehouseId = bin.Warehouse.Id, SectionId = bin.Section.Id, BinId = bin.Bin.Id,
            StockType = "physical", Quantity = 0, ReservedQuantity = 0,
        };
        db.Stocks.Add(st);
        return st;
    }

    /// <summary>K-14: miktar ve rezerve 0 ise satır silinir.</summary>
    public static void BosSatiriKaldir(IInventoryDbContext db, Stock st)
    {
        if (st.Quantity == 0 && st.ReservedQuantity == 0) db.Stocks.Remove(st);
    }

    public static StockMovement Hareket(Guid variantId, string tip, int qty, Guid? fromWh, Guid? fromBin, Guid? toWh, Guid? toBin,
        string? refType, Guid? refId, string? notes, Guid? by)
        => new()
        {
            VariantId = variantId, MovementType = tip, Quantity = qty,
            FromWarehouseId = fromWh, FromBinId = fromBin, ToWarehouseId = toWh, ToBinId = toBin,
            ReferenceType = refType, ReferenceId = refId, Notes = notes, CreatedBy = by,
        };

    /// <summary>
    /// Göz→göz taşıma çekirdeği (aynı depo; kilit ALTINDA çağrılır). <paramref name="qty"/> null → satırın tamamı
    /// (rezervasyonlarla birlikte); değilse yalnız serbest adetten (<c>Quantity − Reserved</c>) düşer, rezervasyonlar
    /// kaynakta kalır. Taşınan adedi döner; hata metni varsa taşıma yapılmaz.
    /// </summary>
    public static async Task<(int Moved, string? Error)> MoveAsync(
        IInventoryDbContext db, Guid variantId, BinInfo from, BinInfo to, int? qty, string movementType,
        string? refType, Guid? refId, string? notes, Guid? by, CancellationToken ct)
    {
        if (from.Warehouse.Id != to.Warehouse.Id) return (0, "Göz→göz taşıma yalnız aynı depo içinde yapılabilir; depolar arası için transfer/mağaza taşıma kullanın.");
        if (from.Bin.Id == to.Bin.Id) return (0, "Kaynak ve hedef göz aynı.");
        var src = await db.Stocks.FirstOrDefaultAsync(s => s.VariantId == variantId && s.BinId == from.Bin.Id, ct);
        if (src is null || src.Quantity <= 0) return (0, "Ürün kaynak gözde yok.");

        var dst = await GetOrCreateAsync(db, variantId, to, ct);
        int moved;
        if (qty is null)
        {
            // Satır bütünüyle: miktar + rezervasyonlar hedefe geçer (K2 — toplayıcı yeni gözü görür)
            moved = src.Quantity;
            var rezervasyonlar = await db.StockReservations.Where(r => r.StockId == src.Id && r.Status == "reserved").ToListAsync(ct);
            foreach (var r in rezervasyonlar) r.StockId = dst.Id;   // yeni satırın Id'si BaseEntity'de üretildi
            dst.Quantity += src.Quantity; dst.ReservedQuantity += src.ReservedQuantity;
            src.Quantity = 0; src.ReservedQuantity = 0;
        }
        else
        {
            if (qty.Value <= 0) return (0, "Adet 0'dan büyük olmalı.");
            var serbest = src.Quantity - src.ReservedQuantity;
            if (qty.Value > serbest)
                return (0, $"Kaynak gözde serbest adet {serbest} (rezerve {src.ReservedQuantity}); {qty.Value} taşınamaz. Rezervasyonlar yalnız 'tümünü taşı' ile hedefe geçer.");
            moved = qty.Value;
            src.Quantity -= moved; dst.Quantity += moved;
        }
        src.UpdatedAt = DateTime.UtcNow; dst.UpdatedAt = DateTime.UtcNow;
        db.StockMovements.Add(Hareket(variantId, movementType, moved, from.Warehouse.Id, from.Bin.Id, to.Warehouse.Id, to.Bin.Id, refType, refId, notes, by));
        BosSatiriKaldir(db, src);
        return (moved, null);
    }
}
