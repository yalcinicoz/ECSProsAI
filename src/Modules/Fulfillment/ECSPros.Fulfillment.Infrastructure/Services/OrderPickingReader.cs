using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Infrastructure.Services;

/// <summary>OP1: Order/Inventory/Catalog şemalarından toplama adayı okur (salt-okunur,
/// raw SQL — OrderPackagingReader kalıbı). Depo "tümü aynı depoda" kuralı bellek tarafında.</summary>
public class OrderPickingReader(FulfillmentDbContext db) : IOrderPickingReader
{
    private sealed class CandidateRow
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public Guid FirmPlatformId { get; set; }
        public Guid ShippingCityId { get; set; }
        public Guid? RequestedCargoIntegrationId { get; set; }
        public string? RequestedCargoName { get; set; }
        public DateTime CreatedAt { get; set; }
        public int TotalQuantity { get; set; }
    }

    private sealed class ReservationRow
    {
        public Guid OrderId { get; set; }
        public Guid VariantId { get; set; }
        public int Quantity { get; set; }
        public Guid WarehouseId { get; set; }
        public Guid? BinId { get; set; }
        public string? BinCode { get; set; }
        public int SectionOrder { get; set; }
        public int BinOrder { get; set; }
    }

    private sealed class ItemRow
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public Guid VariantId { get; set; }
        public int Quantity { get; set; }
        public string? Barcode { get; set; }
        public string? Sku { get; set; }
        public string? ProductName { get; set; }
        public string? VariantInfo { get; set; }
    }

    public async Task<List<PickingCandidate>> GetCandidatesAsync(PickingTaskFilter filter, CancellationToken ct = default)
    {
        var kanallar = filter.FirmPlatformIds is { Count: > 0 } ? filter.FirmPlatformIds.ToArray() : null;

        var orders = await db.Database.SqlQuery<CandidateRow>($"""
            SELECT o."Id", o."OrderNumber", o."FirmPlatformId", o."ShippingCityId",
                   o."RequestedCargoIntegrationId", o."RequestedCargoName", o."CreatedAt",
                   (SELECT COALESCE(SUM(i."Quantity"), 0)::int FROM "order".ord_order_items i
                     WHERE i."OrderId" = o."Id" AND i."IsDeleted" = false) AS "TotalQuantity"
            FROM "order".ord_orders o
            WHERE o."Status" = 'confirmed' AND o."IsDeleted" = false
              AND o."PickingPlanId" IS NULL
              AND ({kanallar}::uuid[] IS NULL OR o."FirmPlatformId" = ANY({kanallar}::uuid[]))
              AND ({filter.CargoIntegrationId}::uuid IS NULL OR o."RequestedCargoIntegrationId" = {filter.CargoIntegrationId})
              AND ({filter.ShippingCityId}::uuid IS NULL OR o."ShippingCityId" = {filter.ShippingCityId})
              AND ({filter.From}::timestamptz IS NULL OR o."CreatedAt" >= {filter.From})
              AND ({filter.To}::timestamptz IS NULL OR o."CreatedAt" <= {filter.To})
            ORDER BY o."CreatedAt"
            """).ToListAsync(ct);

        if (filter.MinItems is { } min) orders = orders.Where(o => o.TotalQuantity >= min).ToList();
        if (filter.MaxItems is { } max) orders = orders.Where(o => o.TotalQuantity <= max).ToList();
        if (orders.Count == 0) return [];

        var ids = orders.Select(o => o.Id).ToArray();
        var rezervDepolar = await db.Database.SqlQuery<ReservationRow>($"""
            SELECT r."ReferenceId" AS "OrderId", r."VariantId", r."Quantity", r."WarehouseId",
                   NULL::uuid AS "BinId", NULL::text AS "BinCode", 0 AS "SectionOrder", 0 AS "BinOrder"
            FROM inventory.inv_stock_reservations r
            WHERE r."ReferenceType" = 'order' AND r."Status" = 'reserved'
              AND r."IsDeleted" = false AND r."ReferenceId" = ANY({ids})
            """).ToListAsync(ct);

        var depoByOrder = rezervDepolar
            .GroupBy(r => r.OrderId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.WarehouseId).Distinct().ToList());

        var sonuc = orders.Select(o => new PickingCandidate(
                o.Id, o.OrderNumber, o.FirmPlatformId, o.ShippingCityId,
                o.RequestedCargoIntegrationId, o.RequestedCargoName, o.CreatedAt,
                o.TotalQuantity, depoByOrder.GetValueOrDefault(o.Id) ?? []))
            .ToList();

        // Depo filtresi: yalnız TÜM rezervasyonları o depoda olan siparişler (K: karma hariç)
        if (filter.WarehouseId is { } depo)
            sonuc = sonuc.Where(c => c.WarehouseIds.Count == 1 && c.WarehouseIds[0] == depo).ToList();

        return sonuc;
    }

    public async Task<PickingLineSource> GetLineSourcesAsync(List<Guid> orderIds, CancellationToken ct = default)
    {
        if (orderIds.Count == 0) return new PickingLineSource([], []);
        var ids = orderIds.ToArray();

        var items = await db.Database.SqlQuery<ItemRow>($"""
            SELECT i."Id", i."OrderId", i."VariantId", i."Quantity", v."Barcode",
                   i."Sku", i."ProductName", i."VariantInfo"
            FROM "order".ord_order_items i
            LEFT JOIN catalog.product_variants v ON v."Id" = i."VariantId" AND v."IsDeleted" = false
            WHERE i."OrderId" = ANY({ids}) AND i."IsDeleted" = false
            """).ToListAsync(ct);

        var rezervler = await db.Database.SqlQuery<ReservationRow>($"""
            SELECT r."ReferenceId" AS "OrderId", r."VariantId", r."Quantity", r."WarehouseId",
                   s."BinId", b."Code" AS "BinCode",
                   COALESCE(sec."PickingOrder", 0) AS "SectionOrder",
                   COALESCE(b."PickingOrder", 0) AS "BinOrder"
            FROM inventory.inv_stock_reservations r
            JOIN inventory.inv_stocks s ON s."Id" = r."StockId"
            LEFT JOIN inventory.inv_warehouse_bins b ON b."Id" = s."BinId"
            LEFT JOIN inventory.inv_warehouse_sections sec ON sec."Id" = s."SectionId"
            WHERE r."ReferenceType" = 'order' AND r."Status" = 'reserved'
              AND r."IsDeleted" = false AND r."ReferenceId" = ANY({ids})
            """).ToListAsync(ct);

        return await MapAsync(items, rezervler);
    }

    public async Task<BinInfo?> GetBinByBarcodeAsync(string barcode, CancellationToken ct = default)
    {
        var raflar = await db.Database.SqlQuery<BinRow>($"""
            SELECT b."Id", b."Code" FROM inventory.inv_warehouse_bins b
            WHERE (b."Barcode" = {barcode} OR b."Code" = {barcode}) AND b."IsActive" AND b."IsDeleted" = false
            LIMIT 1
            """).ToListAsync(ct);
        return raflar.Count == 0 ? null : new BinInfo(raflar[0].Id, raflar[0].Code);
    }

    private sealed class BinRow
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
    }

    private static Task<PickingLineSource> MapAsync(List<ItemRow> items, List<ReservationRow> rezervler)
    {
        return Task.FromResult(new PickingLineSource(
            items.Select(i => new PickingItemRow(i.Id, i.OrderId, i.VariantId, i.Quantity, i.Barcode,
                i.Sku, i.ProductName, i.VariantInfo)).ToList(),
            rezervler.Select(r => new PickingReservationRow(
                r.OrderId, r.VariantId, r.Quantity, r.BinId, r.BinCode, r.SectionOrder, r.BinOrder)).ToList()));
    }
}
