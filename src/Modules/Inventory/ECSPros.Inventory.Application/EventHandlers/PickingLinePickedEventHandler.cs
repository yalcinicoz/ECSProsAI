using ECSPros.Fulfillment.Domain.Events;
using ECSPros.Inventory.Application.Services;
using ECSPros.Inventory.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.EventHandlers;

/// <summary>
/// OP2: toplama okutması stok senkronu — kalemin 'order' tipli rezervasyonu 'picked' yapılır,
/// stok FİİLİ toplanan raftan düşer (K-15; fiili rafta stok satırı yoksa rezervasyon rafından),
/// K-14 gereği 0'a düşen satır silinir, StockMovement izi atılır. OrderShipped handler'ı
/// 'reserved' kalanları işlemeye devam eder (çifte düşüm olmaz: burada picked'e çevrilir).
/// </summary>
public class PickingLinePickedEventHandler(IInventoryDbContext db)
    : INotificationHandler<PickingLinePickedEvent>
{
    public async Task Handle(PickingLinePickedEvent notification, CancellationToken ct)
    {
        foreach (var item in notification.Items)
        {
            var kalanMiktar = item.Quantity;
            var rezervler = await db.StockReservations
                .Where(r => r.ReferenceType == "order" && r.ReferenceId == item.OrderId
                            && r.VariantId == item.VariantId && r.Status == "reserved")
                .ToListAsync(ct);

            foreach (var rezerv in rezervler)
            {
                if (kalanMiktar <= 0) break;
                var dusulecek = Math.Min(kalanMiktar, rezerv.Quantity);

                // ReservedQuantity her zaman rezervasyonun tutulduğu satırdan düşer
                var rezervStok = await db.Stocks.FirstOrDefaultAsync(s => s.Id == rezerv.StockId, ct);
                if (rezervStok is not null)
                    rezervStok.ReservedQuantity = Math.Max(0, rezervStok.ReservedQuantity - dusulecek);

                // Fiziksel miktar FİİLİ raftan düşer (K-15) — fiili rafta satır yoksa rezerv rafından
                Stock? fiiliStok = null;
                if (item.PickedBinId is { } bin && rezervStok?.BinId != bin)
                    fiiliStok = await db.Stocks.FirstOrDefaultAsync(
                        s => s.VariantId == item.VariantId && s.BinId == bin, ct);
                var dususStok = fiiliStok ?? rezervStok;
                if (dususStok is not null)
                {
                    dususStok.Quantity = Math.Max(0, dususStok.Quantity - dusulecek);
                    db.StockMovements.Add(new StockMovement
                    {
                        VariantId = item.VariantId,
                        FromWarehouseId = dususStok.WarehouseId,
                        MovementType = "sale",
                        Quantity = dusulecek,
                        ReferenceType = "order_pick",
                        ReferenceId = item.OrderId,
                        Notes = $"Toplama okutması (raf: {item.PickedBinId?.ToString() ?? "rezerv"})",
                        CreatedBy = notification.ActorId
                    });
                }

                // K-14: 0'a düşen satır(lar) silinir
                foreach (var s in new[] { rezervStok, fiiliStok }.Where(s => s is not null).Distinct())
                    if (s!.Quantity == 0 && s.ReservedQuantity == 0)
                        db.Stocks.Remove(s);

                if (dusulecek >= rezerv.Quantity)
                    rezerv.Status = "picked";
                else
                    rezerv.Quantity -= dusulecek; // kısmi: kalan miktar rezervde bekler (K-17)
                kalanMiktar -= dusulecek;
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
