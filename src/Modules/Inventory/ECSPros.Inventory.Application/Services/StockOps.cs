using ECSPros.Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Services;

/// <summary>
/// Üçlü depo (Depo → Kısım → Birim/Raf) stok işlemleri — handler cutover'ın merkezi (2026-07-14).
/// Stok satırı artık (VariantId, BinId) başına; SectionId/WarehouseId denormalize. Satışa-açıklık
/// KISIM seviyesinde (<see cref="WarehouseSection.IsSellableOnline"/>). Olaylar raf belirtmediği
/// için raf seçimi burada yapılır:
///  - Rezerve/tüket: verilen deponun rafları, satışa-açık kısımlar önce, picking sırasıyla, greedy.
///  - Al (iade/adjust+): mevcut varyant rafı (picking önceliği) → yoksa deponun varsayılan rafı;
///    iadeler için satışa-KAPALI kısım (İade/Defo) tercih edilir (muayene için).
/// Handler cutover ERTELENMİŞTİ; stok kontrolü açılınca canlıya yansır. Eski (variant,warehouse)
/// tek-satır mantığının yerini alır.
/// </summary>
public static class StockOps
{
    // Online satılabilir serbest stok: yalnız satışa-açık kısımların (aktif depo) rafları.
    public static async Task<int> AvailableOnlineAsync(IInventoryDbContext db, Guid variantId, CancellationToken ct)
    {
        var q = from s in db.Stocks
                where s.VariantId == variantId && s.BinId != null
                join sec in db.WarehouseSections on s.SectionId equals sec.Id
                join w in db.Warehouses on s.WarehouseId equals w.Id
                where sec.IsSellableOnline && w.IsActive
                select (int?)(s.Quantity - s.ReservedQuantity);
        return await q.SumAsync(ct) ?? 0;
    }

    // Tüm varyantların online satılabilir serbest stoğu (facet/liste için toplu).
    public static async Task<Dictionary<Guid, int>> AllAvailableOnlineAsync(IInventoryDbContext db, CancellationToken ct)
    {
        var rows = await (from s in db.Stocks
                          where s.BinId != null
                          join sec in db.WarehouseSections on s.SectionId equals sec.Id
                          join w in db.Warehouses on s.WarehouseId equals w.Id
                          where sec.IsSellableOnline && w.IsActive
                          group (s.Quantity - s.ReservedQuantity) by s.VariantId into g
                          select new { VariantId = g.Key, Available = g.Sum() })
                         .ToListAsync(ct);
        return rows.ToDictionary(r => r.VariantId, r => r.Available);
    }

    // Bir depodaki varyant stok satırları — satışa-açık kısımlar önce, picking sırasıyla (tahsis/tüketim için).
    private static async Task<List<Stock>> BinStocksOrderedAsync(
        IInventoryDbContext db, Guid variantId, Guid warehouseId, CancellationToken ct)
    {
        return await (from s in db.Stocks
                      where s.VariantId == variantId && s.WarehouseId == warehouseId && s.BinId != null
                      join sec in db.WarehouseSections on s.SectionId equals sec.Id
                      join b in db.WarehouseBins on s.BinId equals b.Id
                      orderby sec.IsSellableOnline descending, sec.PickingOrder, b.PickingOrder, b.Id
                      select s).ToListAsync(ct);
    }

    // ── REZERVE (OrderConfirmed): deponun raflarından greedy tahsis + raf-seviyesi rezervasyon.
    public static async Task ReserveAsync(
        IInventoryDbContext db, Guid variantId, Guid warehouseId, int qty,
        string referenceType, Guid referenceId, CancellationToken ct)
    {
        if (qty <= 0) return;
        var rows = await BinStocksOrderedAsync(db, variantId, warehouseId, ct);
        int remaining = qty;
        foreach (var st in rows)
        {
            if (remaining <= 0) break;
            int free = st.Quantity - st.ReservedQuantity;
            if (free <= 0) continue;
            int take = Math.Min(remaining, free);
            st.ReservedQuantity += take;
            db.StockReservations.Add(NewReservation(st, variantId, warehouseId, take, referenceType, referenceId));
            remaining -= take;
        }
        if (remaining > 0)
        {
            // Yetersiz serbest stok — kalanı ilk rafa (yoksa varsayılan rafa) fazladan rezerve et
            // ki rezervasyon toplamı = qty olsun. (Stok kontrolü kapalıyken over-reserve kabul; açılınca
            // checkout zaten yeterli stoğu doğrulayacak.)
            var target = rows.FirstOrDefault() ?? await EnsureReceivingStockAsync(db, variantId, warehouseId, false, ct);
            if (target is not null)
            {
                target.ReservedQuantity += remaining;
                db.StockReservations.Add(NewReservation(target, variantId, warehouseId, remaining, referenceType, referenceId));
            }
        }
    }

    // ── TÜKET (POS satışı): deponun raflarından greedy düş (negatife düşürmeden).
    public static async Task ConsumeAsync(
        IInventoryDbContext db, Guid variantId, Guid warehouseId, int qty, CancellationToken ct)
    {
        if (qty <= 0) return;
        var rows = await BinStocksOrderedAsync(db, variantId, warehouseId, ct);
        int remaining = qty;
        foreach (var st in rows)
        {
            if (remaining <= 0) break;
            int take = Math.Min(remaining, st.Quantity);
            if (take <= 0) continue;
            st.Quantity -= take;
            remaining -= take;
        }
        // remaining > 0 ise depoda yeterli fiziksel stok yoktu — POS anlık satışta bu kabul edilir
        // (eski handler de Math.Max(0,...) ile yutuyordu); satır oluşturmuyoruz.
    }

    // ── AL (iade/POS iadesi/adjust+): stoğu bir rafa ekle. preferReturns=true → satışa-kapalı kısım (İade).
    public static async Task ReceiveAsync(
        IInventoryDbContext db, Guid variantId, Guid warehouseId, int qty, bool preferReturns, CancellationToken ct)
    {
        if (qty <= 0) return;
        // Öncelik: varyantın bu depoda mevcut rafı (iade değilse satışa-açık önce; iadeyse kapalı önce).
        var existing = await (from s in db.Stocks
                              where s.VariantId == variantId && s.WarehouseId == warehouseId && s.BinId != null
                              join sec in db.WarehouseSections on s.SectionId equals sec.Id
                              orderby (sec.IsSellableOnline == !preferReturns) descending, sec.PickingOrder
                              select s).FirstOrDefaultAsync(ct);
        var target = existing ?? await EnsureReceivingStockAsync(db, variantId, warehouseId, preferReturns, ct);
        if (target is not null) target.Quantity += qty;
    }

    // Varyantın bu depoda stok satırı yoksa: uygun bir rafta (iade→kapalı kısım, değilse açık kısım
    // önce) 0 miktarlı satır oluşturur. Depoda hiç raf yoksa BinId'siz eski-tarz satıra düşer (miktar
    // kaybolmasın). Döndürdüğü satır TRACKED — çağıran Quantity/ReservedQuantity'yi artırır.
    private static async Task<Stock?> EnsureReceivingStockAsync(
        IInventoryDbContext db, Guid variantId, Guid warehouseId, bool preferReturns, CancellationToken ct)
    {
        var bin = await (from b in db.WarehouseBins
                         join sec in db.WarehouseSections on b.SectionId equals sec.Id
                         where sec.WarehouseId == warehouseId && b.IsActive && sec.IsActive
                         orderby (sec.IsSellableOnline == !preferReturns) descending, sec.PickingOrder, b.PickingOrder, b.Id
                         select new { BinId = b.Id, b.SectionId }).FirstOrDefaultAsync(ct);

        var stock = new Stock
        {
            VariantId = variantId,
            WarehouseId = warehouseId,
            SectionId = bin?.SectionId,
            BinId = bin?.BinId,   // depoda raf yoksa null (eski-tarz fallback — miktar korunur)
            StockType = "physical",
            Quantity = 0,
            ReservedQuantity = 0
        };
        db.Stocks.Add(stock);
        await db.SaveChangesAsync(ct);   // Id + FK'ler için (rezervasyon StockId'ye bağlanır)
        return stock;
    }

    private static StockReservation NewReservation(
        Stock st, Guid variantId, Guid warehouseId, int qty, string referenceType, Guid referenceId) =>
        new()
        {
            StockId = st.Id,
            VariantId = variantId,
            WarehouseId = warehouseId,
            LocationId = null,
            Quantity = qty,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            Status = "reserved"
        };
}
