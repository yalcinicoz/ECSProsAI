using ECSPros.Api.Services.LegacyStock;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class LegacyStockSyncOptionsTests
{
    [TestMethod]
    public void Varsayilanlar_GercekYazimiKapaliTutar()
    {
        var options = new LegacyStockSyncOptions();

        options.Validate();

        Assert.IsFalse(options.Enabled);
        Assert.IsTrue(options.DryRun);
        Assert.AreEqual(300, options.IntervalSeconds);
        Assert.AreEqual(1, options.StockStorageType);
        Assert.IsTrue(options.BlockOnUnmappedQuantity);
        Assert.AreEqual(0, options.MaximumUnmappedRows);
        Assert.AreEqual(0L, options.MaximumUnmappedQuantity);
        Assert.IsFalse(options.RepairMissingMappings);
        Assert.IsTrue(options.MappingRepairDryRun);
    }

    [TestMethod]
    public void DusukKaynakEsigi_Reddedilir()
    {
        var options = new LegacyStockSyncOptions { MinimumSourceRows = 0 };

        var error = Assert.ThrowsExactly<InvalidOperationException>(options.Validate);

        StringAssert.Contains(error.Message, "MinimumSourceRows");
    }

    [TestMethod]
    public void NegatifEslesmemeSiniri_Reddedilir()
    {
        var options = new LegacyStockSyncOptions { MaximumUnmappedQuantity = -1 };

        var error = Assert.ThrowsExactly<InvalidOperationException>(options.Validate);

        StringAssert.Contains(error.Message, "MaximumUnmappedQuantity");
    }
}
