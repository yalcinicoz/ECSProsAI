using ECSPros.Accounts.Application.Services;
using ECSPros.Accounts.Domain.Entities;
using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.Marketplace.Commission;

/// <summary>
/// P3a (2026-08-11): teslimde satıcı hakediş satırı üretimi — teslim ŞART kararı.
/// Satır kalem başınadır; oran + katman izi + kampanya paylaşımı yazılır. Bakiye etkisi
/// burada YOKTUR: satırlar 'pending' doğar, uygunlaşınca (teslim + sözleşme X)
/// SettlementEligibilityWorker defter kaydını atar (cari çatı altın kuralı).
/// Handler host'tadır — Accounts/Order/Catalog/Promotion tek yerde görülür.
/// </summary>
public sealed class OrderDeliveredSettlementHandler(
    IOrderDbContext orderDb,
    IAccountsDbContext accountsDb,
    KomisyonCozucu cozucu,
    ILogger<OrderDeliveredSettlementHandler> logger) : INotificationHandler<OrderDeliveredEvent>
{
    public async Task Handle(OrderDeliveredEvent e, CancellationToken ct)
    {
        try
        {
            var order = await orderDb.Orders.AsNoTracking()
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == e.OrderId, ct);
            if (order is null) return;

            var saticiKalemleri = order.Items.Where(i => i.SupplierId != null).ToList();
            if (saticiKalemleri.Count == 0) return;

            // Zaten üretilmiş satırlar (idempotency — teslim çift işaretlenirse ikinci tur atlanır)
            var kalemIdler = saticiKalemleri.Select(i => i.Id).ToList();
            var mevcutlar = await accountsDb.SettlementLines.AsNoTracking()
                .Where(l => kalemIdler.Contains(l.OrderItemId) && l.ReversalOfId == null)
                .Select(l => l.OrderItemId)
                .ToListAsync(ct);
            var yeniKalemler = saticiKalemleri.Where(i => !mevcutlar.Contains(i.Id)).ToList();
            if (yeniKalemler.Count == 0) return;

            // Kalem-kampanya bağı: OrderDiscounts (kalem bazlı, kampanya kaynaklı)
            var indirimler = await orderDb.OrderDiscounts.AsNoTracking()
                .Where(d => d.OrderId == e.OrderId && d.OrderItemId != null && d.DiscountSourceId != null)
                .Select(d => new { d.OrderItemId, d.DiscountSourceId })
                .ToListAsync(ct);
            var kampanyaByKalem = indirimler
                .GroupBy(d => d.OrderItemId!.Value)
                .ToDictionary(g => g.Key, g => g.First().DiscountSourceId);

            var uretilen = 0;
            foreach (var grup in yeniKalemler.GroupBy(i => i.SupplierId!.Value))
            {
                var girdiler = grup.Select(i => new KomisyonCozucu.KalemGirdisi(
                    i.Id, i.VariantId, i.Total, i.DiscountAmount,
                    kampanyaByKalem.GetValueOrDefault(i.Id))).ToList();
                var kararlar = await cozucu.CozAsync(grup.Key, girdiler, e.OccurredAt, ct);
                var kararById = kararlar.ToDictionary(k => k.OrderItemId);

                foreach (var kalem in grup)
                {
                    if (!kararById.TryGetValue(kalem.Id, out var karar)) continue;
                    var net = kalem.Total - karar.CommissionAmount - karar.DiscountShareAmount;
                    accountsDb.SettlementLines.Add(new SettlementLine
                    {
                        SupplierAccountId = grup.Key,
                        OrderId = order.Id,
                        OrderItemId = kalem.Id,
                        OrderNumber = order.OrderNumber,
                        Sku = kalem.Sku,
                        ProductName = kalem.ProductName,
                        Quantity = kalem.Quantity,
                        GrossAmount = kalem.Total,
                        CommissionRate = karar.Rate,
                        CommissionLayer = karar.Layer,
                        CommissionAmount = karar.CommissionAmount,
                        CampaignDiscountShareAmount = karar.DiscountShareAmount,
                        NetAmount = net,
                        CampaignId = karar.CampaignId,
                        Status = "pending",
                        DeliveredAt = e.OccurredAt,
                        EligibleAt = e.OccurredAt.AddDays(karar.SettlementDelayDays)
                    });
                    uretilen++;
                }
            }
            await accountsDb.SaveChangesAsync(ct);
            logger.LogInformation("Hakediş: {Siparis} için {Adet} satır üretildi.", order.OrderNumber, uretilen);
        }
        catch (Exception ex)
        {
            // Teslim akışını düşürme — hakediş üretimi telafi edilebilir (panelden yeniden tetiklenebilir)
            logger.LogError(ex, "Hakediş satırı üretilemedi (sipariş {OrderId}).", e.OrderId);
        }
    }
}

/// <summary>
/// İade alındığında (received) hakediş tersi: ilgili kalemlerin satırlarına ORANSAL ters satır
/// yazılır (miktar payı). Orijinal defterlenmişse worker ters kaydı da defterler (negatif net →
/// Debit). Aynı iade için ikinci kez ters satır üretilmez (ReturnId referansı Description'da).
/// </summary>
public sealed class ReturnReceivedSettlementHandler(
    IOrderDbContext orderDb,
    IAccountsDbContext accountsDb,
    ILogger<ReturnReceivedSettlementHandler> logger) : INotificationHandler<ReturnReceivedEvent>
{
    public async Task Handle(ReturnReceivedEvent e, CancellationToken ct)
    {
        try
        {
            var kalemler = await orderDb.OrderItems.AsNoTracking()
                .Where(i => i.OrderId == e.OrderId && i.SupplierId != null)
                .Select(i => new { i.Id, i.VariantId, i.Quantity })
                .ToListAsync(ct);
            if (kalemler.Count == 0) return;

            var iadeByVariant = e.Items.GroupBy(i => i.VariantId).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
            var etkilenen = kalemler.Where(k => iadeByVariant.ContainsKey(k.VariantId)).ToList();
            if (etkilenen.Count == 0) return;

            var kalemIdler = etkilenen.Select(k => k.Id).ToList();
            var satirlar = await accountsDb.SettlementLines
                .Where(l => kalemIdler.Contains(l.OrderItemId) && l.ReversalOfId == null && l.Status != "reversed")
                .ToListAsync(ct);

            var iadeRef = $"return:{e.ReturnId}";
            var zatenVar = await accountsDb.SettlementLines.AsNoTracking()
                .AnyAsync(l => l.Description == iadeRef, ct);
            if (zatenVar) return; // aynı iade ikinci kez işlenmez

            var uretilen = 0;
            foreach (var satir in satirlar)
            {
                var kalem = etkilenen.First(k => k.Id == satir.OrderItemId);
                var iadeAdet = Math.Min(iadeByVariant[kalem.VariantId], satir.Quantity);
                if (iadeAdet <= 0) continue;
                var oran = (decimal)iadeAdet / satir.Quantity;
                var tamIade = iadeAdet == satir.Quantity;

                // Orijinal henüz DEFTERLENMEMİŞSE (pending) ve iade TAM ise: ikisi de deftere
                // hiç girmez — orijinal 'reversed', ters satır kayıt izi olarak 'reversed' doğar.
                // Aksi halde ters satır 'pending' doğar; worker orijinal defterlenmeden tersini
                // işlemez (sıra garantisi worker'da).
                var tersDefterlenecek = !(satir.Status == "pending" && tamIade);
                if (satir.Status == "pending" && tamIade)
                    satir.Status = "reversed";

                accountsDb.SettlementLines.Add(new SettlementLine
                {
                    SupplierAccountId = satir.SupplierAccountId,
                    OrderId = satir.OrderId,
                    OrderItemId = satir.OrderItemId,
                    OrderNumber = satir.OrderNumber,
                    Sku = satir.Sku,
                    ProductName = satir.ProductName,
                    Quantity = -iadeAdet,
                    GrossAmount = -Math.Round(satir.GrossAmount * oran, 2),
                    CommissionRate = satir.CommissionRate,
                    CommissionLayer = satir.CommissionLayer,
                    CommissionAmount = -Math.Round(satir.CommissionAmount * oran, 2),
                    CampaignDiscountShareAmount = -Math.Round(satir.CampaignDiscountShareAmount * oran, 2),
                    NetAmount = -Math.Round(satir.NetAmount * oran, 2),
                    CampaignId = satir.CampaignId,
                    Status = tersDefterlenecek ? "pending" : "reversed",
                    DeliveredAt = satir.DeliveredAt,
                    EligibleAt = DateTime.UtcNow,
                    AvailableAt = tersDefterlenecek ? null : DateTime.UtcNow,
                    ReversalOfId = satir.Id,
                    Description = iadeRef
                });
                uretilen++;
            }
            if (uretilen > 0)
            {
                await accountsDb.SaveChangesAsync(ct);
                logger.LogInformation("Hakediş iade tersi: {Ref} için {Adet} satır.", iadeRef, uretilen);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Hakediş iade tersi üretilemedi (sipariş {OrderId}).", e.OrderId);
        }
    }
}
