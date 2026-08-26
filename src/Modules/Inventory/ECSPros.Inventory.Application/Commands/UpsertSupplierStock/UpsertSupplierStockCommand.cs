using ECSPros.Inventory.Application.Services;
using ECSPros.Inventory.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Commands.UpsertSupplierStock;

/// <summary>Partner `PUT /stock` — tedarikçi kendi ürünlerinin stoğunu MUTLAK olarak bildirir.
/// Stok "Tedarikçi Stokları" deposunda tedarikçiye özel KISIM'da tutulur (IsSellableOnline=true →
/// online mevcudiyete sayılır, StockOps.AvailableOnlineAsync). Depo/kısım/raf ilk bildirimde
/// otomatik açılır. (F2b-2b, docs §3.7)</summary>
public record UpsertSupplierStockCommand(Guid SupplierId, IReadOnlyList<SupplierStockItem> Items)
    : IRequest<Result<int>>;

public record SupplierStockItem(Guid VariantId, int Quantity);

public class UpsertSupplierStockCommandHandler : IRequestHandler<UpsertSupplierStockCommand, Result<int>>
{
    private const string SupplierWarehouseCode = "SUPPLIER_STOCK";
    private readonly IInventoryDbContext _db;

    public UpsertSupplierStockCommandHandler(IInventoryDbContext db) => _db = db;

    public async Task<Result<int>> Handle(UpsertSupplierStockCommand request, CancellationToken ct)
    {
        if (request.Items.Count == 0) return Result.Success(0);

        // 1) "Tedarikçi Stokları" deposu (find-or-create)
        var warehouse = await _db.Warehouses.FirstOrDefaultAsync(w => w.Code == SupplierWarehouseCode, ct);
        if (warehouse is null)
        {
            warehouse = new Warehouse
            {
                Code = SupplierWarehouseCode,
                NameI18n = new() { ["tr"] = "Tedarikçi Stokları", ["en"] = "Supplier Stock" },
                WarehouseType = "virtual",
                IsSellableOnline = true,
                IsActive = true
            };
            _db.Warehouses.Add(warehouse);
            await _db.SaveChangesAsync(ct);
        }

        // 2) Tedarikçiye özel KISIM (find-or-create; satışa açık)
        var section = await _db.WarehouseSections
            .FirstOrDefaultAsync(s => s.WarehouseId == warehouse.Id && s.SupplierId == request.SupplierId, ct);
        if (section is null)
        {
            section = new WarehouseSection
            {
                WarehouseId = warehouse.Id,
                SupplierId = request.SupplierId,
                Code = $"SUP-{request.SupplierId.ToString("N")[..8].ToUpperInvariant()}",
                Name = $"Tedarikçi {request.SupplierId.ToString("N")[..8].ToUpperInvariant()}",
                IsSellableOnline = true,
                IsActive = true
            };
            _db.WarehouseSections.Add(section);
            await _db.SaveChangesAsync(ct);
        }

        // 3) Varsayılan raf (find-or-create)
        var bin = await _db.WarehouseBins.FirstOrDefaultAsync(b => b.SectionId == section.Id, ct);
        if (bin is null)
        {
            bin = new WarehouseBin
            {
                SectionId = section.Id,
                Code = "DEFAULT",
                Barcode = $"SUPBIN-{section.Id.ToString("N")[..12].ToUpperInvariant()}",
                Name = "Varsayılan",
                IsActive = true
            };
            _db.WarehouseBins.Add(bin);
            await _db.SaveChangesAsync(ct);
        }

        // 4) Stok upsert — MUTLAK miktar (tedarikçi mevcut seviyeyi bildirir)
        // Faz 0 (StockTx): varyant kilitleri altında — rezervasyon/tüketimle yarışı serileştirir.
        var variantIds = request.Items.Select(i => i.VariantId).ToList();
        var whId = warehouse.Id; var secId = section.Id; var binId = bin.Id;   // Clear() sonrası detached — yalnız Id kullan
        await StockTx.RunAsync(_db, variantIds, async () =>
        {
        var existing = await _db.Stocks
            .Where(s => s.SectionId == secId && variantIds.Contains(s.VariantId))
            .ToListAsync(ct);
        var byVariant = existing.ToDictionary(s => s.VariantId);

        foreach (var item in request.Items)
        {
            var qty = Math.Max(0, item.Quantity);
            if (byVariant.TryGetValue(item.VariantId, out var st))
            {
                st.Quantity = qty;
            }
            else
            {
                _db.Stocks.Add(new Stock
                {
                    VariantId = item.VariantId,
                    WarehouseId = whId,
                    SectionId = secId,
                    BinId = binId,
                    StockType = "physical",
                    Quantity = qty,
                    ReservedQuantity = 0
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        }, ct);
        return Result.Success(request.Items.Count);
    }
}
