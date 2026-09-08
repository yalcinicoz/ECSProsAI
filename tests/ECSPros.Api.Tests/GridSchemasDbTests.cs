using ECSPros.Api.Grid;
using ECSPros.Catalog.Application.Queries.GetProducts;
using ECSPros.Catalog.Infrastructure.Persistence;
using ECSPros.Crm.Application.Queries.GetMembers;
using ECSPros.Crm.Application.Tickets.Queries;
using ECSPros.Crm.Infrastructure.Persistence;
using ECSPros.Inventory.Infrastructure.Persistence;
using ECSPros.Order.Application.Queries.GetInvoices;
using ECSPros.Order.Application.Queries.GetOrders;
using ECSPros.Order.Application.Queries.GetReturns;
using ECSPros.Order.Infrastructure.Persistence;
using ECSPros.Promotion.Application.Queries.GetCampaigns;
using ECSPros.Promotion.Infrastructure.Persistence;
using ECSPros.Shared.Kernel.Grid;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ECSPros.Api.Tests;

/// <summary>
/// DataGrid F4 — TÜM şemaların EF/Npgsql SQL çevirisi (SALT OKUNUR): her filtre alanı tipine uygun örnek değerle, her sıralama anahtarı
/// asc/desc ile gerçek DB'de COUNT/Take(1) çalıştırılır; çevrilemeyen ifade burada patlar. Yalnız ECSPROS_TEST_DB verildiğinde çalışır.
/// </summary>
[TestClass]
public sealed class GridSchemasDbTests
{
    private static NpgsqlDataSource? Ds()
    {
        var cs = Environment.GetEnvironmentVariable("ECSPROS_TEST_DB");
        if (string.IsNullOrWhiteSpace(cs)) return null;
        var b = new NpgsqlDataSourceBuilder(cs); b.EnableDynamicJson(); return b.Build();
    }
    private static DbContextOptions<T> Opt<T>(NpgsqlDataSource ds) where T : DbContext => new DbContextOptionsBuilder<T>().UseNpgsql(ds).Options;

    private static string SampleValue(GridFieldType t, IReadOnlyCollection<string>? allowed) => t switch
    {
        GridFieldType.Text => "a",
        GridFieldType.Enum => allowed is { Count: > 0 } ? string.Join(",", allowed.Take(2)) : "x",
        GridFieldType.Date => "2026-08-01,2026-09-08",
        GridFieldType.Number => "1",
        GridFieldType.Bool => "true",
        GridFieldType.Guid => Guid.Empty.ToString(),
        _ => "1",
    };

    private static async Task<List<string>> ExerciseAsync<T>(string ad, GridSchema<T> schema, IQueryable<T> baseQuery) where T : class
    {
        var hatalar = new List<string>();
        foreach (var key in schema.FilterableFields)
        {
            var type = schema.FieldTypeOf(key)!.Value;
            var req = new GridRequest { Filters = { new GridFilter(key, GridRequest.OpAuto, SampleValue(type, schema.AllowedValuesOf(key))) } };
            try { _ = await schema.ApplyFilters(baseQuery, req).CountAsync(); }
            catch (Exception ex) { hatalar.Add($"{ad}.filter[{key}:{type}] → {ex.GetType().Name}: {ex.Message.Split('\n')[0]}"); }
            // ikinci operatör: text startswith / number between / date gte / enum eq
            var op2 = type switch { GridFieldType.Text => "startswith", GridFieldType.Number => "between", GridFieldType.Date => "gte", GridFieldType.Enum => "eq", _ => null };
            if (op2 is null) continue;
            var val2 = type switch { GridFieldType.Number => "0;10", GridFieldType.Date => "2026-08-01", GridFieldType.Enum => (schema.AllowedValuesOf(key)?.FirstOrDefault() ?? "x"), _ => "a" };
            try { _ = await schema.ApplyFilters(baseQuery, new GridRequest { Filters = { new GridFilter(key, op2, val2) } }).CountAsync(); }
            catch (Exception ex) { hatalar.Add($"{ad}.filter[{key}:{op2}] → {ex.GetType().Name}: {ex.Message.Split('\n')[0]}"); }
        }
        foreach (var key in schema.SortableFields)
            foreach (var dir in new[] { "asc", "desc" })
            {
                try { _ = await schema.ApplySort(baseQuery, new GridRequest { Sort = key, Dir = dir }).Take(1).ToListAsync(); }
                catch (Exception ex) { hatalar.Add($"{ad}.sort[{key} {dir}] → {ex.GetType().Name}: {ex.Message.Split('\n')[0]}"); }
            }
        try { _ = await schema.ApplySort(baseQuery, null).Take(1).ToListAsync(); }
        catch (Exception ex) { hatalar.Add($"{ad}.sort[default] → {ex.Message.Split('\n')[0]}"); }
        return hatalar;
    }

    [TestMethod]
    public async Task All_grid_schemas_translate_to_sql()
    {
        var ds = Ds();
        if (ds is null) { Assert.Inconclusive("ECSPROS_TEST_DB verilmedi — DB testi atlandı."); return; }
        var hatalar = new List<string>();

        await using (var db = new OrderDbContext(Opt<OrderDbContext>(ds)))
        {
            hatalar.AddRange(await ExerciseAsync("orders", OrderGrid.Schema, db.Orders.AsNoTracking()));
            hatalar.AddRange(await ExerciseAsync("invoices", InvoiceGrid.Schema, db.Invoices.AsNoTracking()));
            hatalar.AddRange(await ExerciseAsync("returns", ReturnGrid.Schema, db.Returns.AsNoTracking()));
        }
        await using (var db = new CatalogDbContext(Opt<CatalogDbContext>(ds)))
            hatalar.AddRange(await ExerciseAsync("products", ProductGrid.Schema, db.Products.AsNoTracking()));
        await using (var db = new CrmDbContext(Opt<CrmDbContext>(ds)))
        {
            hatalar.AddRange(await ExerciseAsync("members", MemberGrid.Schema, db.Members.AsNoTracking()));
            hatalar.AddRange(await ExerciseAsync("tickets", TicketGrid.Schema, db.Tickets.AsNoTracking()));
        }
        await using (var db = new InventoryDbContext(Opt<InventoryDbContext>(ds)))
            hatalar.AddRange(await ExerciseAsync("stocks", StockGrid.Schema, db.Stocks.AsNoTracking()));
        await using (var db = new PromotionDbContext(Opt<PromotionDbContext>(ds)))
            hatalar.AddRange(await ExerciseAsync("campaigns", CampaignGrid.Schema, db.Campaigns.AsNoTracking()));

        Assert.AreEqual(0, hatalar.Count, "SQL'e çevrilemeyen alanlar:\n" + string.Join("\n", hatalar));
    }
}
