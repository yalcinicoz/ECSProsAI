using ECSPros.Order.Application.Queries.GetOrders;
using ECSPros.Order.Infrastructure.Persistence;
using ECSPros.Shared.Kernel.Grid;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Tests;

[TestClass]
public class OrderProductFilterTests
{
    private static OrderDbContext Context() => new(new DbContextOptionsBuilder<OrderDbContext>()
        .UseNpgsql("Host=localhost;Database=offline;Username=unused").Options);

    [TestMethod]
    public void UrunFiltreleri_SQLAltSorgusuVeKanalSiniriIleCevirilir()
    {
        using var db = Context();
        var grid = new GridRequest { Filters = { new("barcode", "eq", "8690552190773"), new("productCode", "eq", "P-00019462") }, KanalKisiti = new[] { Guid.NewGuid() } };
        var query = OrderGrid.ApplyAll(db.Orders, new(), grid, db: db);
        var sql = query.ToQueryString();
        StringAssert.Contains(sql, "catalog.product_variants");
        StringAssert.Contains(sql, "catalog.products");
        StringAssert.Contains(sql, "FirmPlatformId");
        StringAssert.Contains(sql, "OrderId");
        StringAssert.Contains(sql, "IsDeleted");
        // Liste sorgusu JOIN ile çoğaltılmaz; eşleşme alt sorguda kalır.
        StringAssert.Contains(sql, "IN (");
    }

    [TestMethod]
    public void GecersizOperatorVeEksikBaglam_SessizceFiltresizDonmez()
    {
        using var db = Context();
        Assert.ThrowsExactly<GridException>(() => OrderGrid.ApplyAll(db.Orders, new(), new GridRequest { Filters = { new("barcode", "contains", "123") } }, db: db));
        Assert.ThrowsExactly<GridException>(() => OrderGrid.ApplyAll(db.Orders, new(), new GridRequest { Filters = { new("barcode", "eq", "123") } }));
        Assert.ThrowsExactly<GridException>(() => OrderGrid.Schema.ApplyFilters(db.Orders, new GridRequest { Filters = { new("barcode", "eq", "123") } }));
    }
}
