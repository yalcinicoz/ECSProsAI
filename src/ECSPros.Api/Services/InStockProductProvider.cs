using ECSPros.Shared.Contracts;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace ECSPros.Api.Services;

/// <summary>
/// Online satılabilir stoğu olan ürün Id kümesi (2026-07-14 — liste stok görünürlüğü).
/// Tek raw-SQL: inv_stocks ↔ satışa-açık kısım/aktif depo ↔ product_variants, ürün başına
/// SUM(serbest) > 0. Aynı DB'de olduğundan cross-schema join tek sorguda. IMemoryCache 2 dk
/// (stok görece stabil; her liste isteğinde tam tarama olmasın). "Stoğu biten" = kümede yok.
/// </summary>
public sealed class InStockProductProvider(NpgsqlDataSource dataSource, IMemoryCache cache) : IInStockProductProvider
{
    private const string ProductCacheKey = "in-stock-product-ids";
    private const string VariantCacheKey = "in-stock-variant-ids";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    private const string ProductSql = @"
        SELECT v.""ProductId""
        FROM inventory.inv_stocks s
        JOIN inventory.inv_warehouse_sections sec ON sec.""Id"" = s.""SectionId""
        JOIN inventory.inv_warehouses w ON w.""Id"" = s.""WarehouseId""
        JOIN catalog.product_variants v ON v.""Id"" = s.""VariantId""
        WHERE s.""BinId"" IS NOT NULL AND sec.""IsSellableOnline"" AND w.""IsActive""
        GROUP BY v.""ProductId""
        HAVING SUM(s.""Quantity"" - s.""ReservedQuantity"") > 0";

    // Varyant düzeyi — renk-modu listelemesinde stoğu biten rengi elemek için (ürün join'i gerekmez).
    private const string VariantSql = @"
        SELECT s.""VariantId""
        FROM inventory.inv_stocks s
        JOIN inventory.inv_warehouse_sections sec ON sec.""Id"" = s.""SectionId""
        JOIN inventory.inv_warehouses w ON w.""Id"" = s.""WarehouseId""
        WHERE s.""BinId"" IS NOT NULL AND sec.""IsSellableOnline"" AND w.""IsActive""
        GROUP BY s.""VariantId""
        HAVING SUM(s.""Quantity"" - s.""ReservedQuantity"") > 0";

    public Task<HashSet<Guid>> GetInStockProductIdsAsync(CancellationToken ct = default) =>
        LoadSetAsync(ProductCacheKey, ProductSql, ct);

    public Task<HashSet<Guid>> GetInStockVariantIdsAsync(CancellationToken ct = default) =>
        LoadSetAsync(VariantCacheKey, VariantSql, ct);

    private async Task<HashSet<Guid>> LoadSetAsync(string cacheKey, string sql, CancellationToken ct)
    {
        return (await cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            var set = new HashSet<Guid>();
            await using var conn = await dataSource.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 30 };
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) set.Add(r.GetGuid(0));
            return set;
        }))!;
    }
}
