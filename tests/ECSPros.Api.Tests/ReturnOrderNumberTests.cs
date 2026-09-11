using ECSPros.Order.Application.Queries.GetReturns;
using ECSPros.Order.Domain.Entities;

namespace ECSPros.Api.Tests;

[TestClass]
public class ReturnOrderNumberTests
{
    [TestMethod]
    public void SiparisNumarasiAramasi_IadeyiBulur_KaynakKimliginiDegistirmez()
    {
        var item = new Return { ReturnNumber = "LRET-221201", Order = new ECSPros.Order.Domain.Entities.Order { OrderNumber = "MIS0000049" } };
        var rows = ReturnGrid.ApplyNamed(new[] { item }.AsQueryable(), new ReturnListFilters(Search: "mis0000049")).ToList();
        Assert.AreEqual(1, rows.Count);
        Assert.AreEqual("LRET-221201", rows[0].ReturnNumber);
        Assert.AreEqual(0, ReturnGrid.ApplyNamed(new[] { item }.AsQueryable(), new ReturnListFilters(Search: "MIS9999999")).Count());
    }
}
