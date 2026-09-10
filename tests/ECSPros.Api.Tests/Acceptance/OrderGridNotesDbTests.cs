using ECSPros.Order.Application.Queries.GetOrders;
using ECSPros.Order.Infrastructure.Persistence;
using ECSPros.Shared.Kernel.Grid;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ECSPros.Api.Tests.Acceptance;

/// <summary>FAZ 15.4h (2026-09-10): CustomerNotes jsonb "note" anahtarı üzerinden filtre/projeksiyon — GridJson.TextObj
/// DbFunction'ının (jsonb parametre tipi) gerçek PostgreSQL'de çevrildiğini doğrular (salt okunur).</summary>
[TestClass]
public sealed class OrderGridNotesDbTests
{
    [TestMethod]
    public async Task Not_filtreleri_ve_projeksiyonu_sunucuda_cevrilir()
    {
        var cs = AcceptanceTestEnvironment.Require("ECSPROS_ACCEPTANCE_ERP_TARGET", "ConnectionStrings:DefaultConnection", "hedef PostgreSQL bağlantısı");
        await using var ds = new NpgsqlDataSourceBuilder(cs).EnableDynamicJson().Build();
        var opt = new DbContextOptionsBuilder<OrderDbContext>().UseNpgsql(ds).Options;
        await using var db = new OrderDbContext(opt);

        var grid = new GridRequest { Page = 1, PageSize = 5, Filters = [new GridFilter("hasNotes", "eq", "true")] };
        var q = OrderGrid.ApplyAll(db.Orders.AsNoTracking(), new OrderListFilters(), grid);
        var notlu = await OrderGrid.Schema.ApplySort(q, grid).Take(5)
            .Select(o => new { o.OrderNumber, Not = GridJson.TextObj(o.CustomerNotes, "note"), o.InternalNotes }).ToListAsync();
        Assert.IsTrue(notlu.All(x => !string.IsNullOrEmpty(x.Not) || !string.IsNullOrEmpty(x.InternalNotes)), "hasNotes=true notsuz satır döndürdü");

        var grid2 = new GridRequest { Page = 1, PageSize = 5, Filters = [new GridFilter("note", "contains", "a")] };
        var q2 = OrderGrid.ApplyAll(db.Orders.AsNoTracking(), new OrderListFilters(), grid2);
        var sayi = await q2.CountAsync();
        Assert.IsTrue(sayi >= 0);
        TestContext.WriteLine($"notlu ilk 5: {notlu.Count}; note contains 'a': {sayi}");
    }

    public TestContext TestContext { get; set; } = null!;
}
