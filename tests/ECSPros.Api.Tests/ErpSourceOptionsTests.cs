using ECSPros.Api.Services.ErpSource;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class ErpSourceOptionsTests
{
    [TestMethod]
    public void VarsayilanAyarlar_UrunOzelligiUzlastirmasiniSinirliTutar()
    {
        var options = new ErpSourceOptions();

        options.Validate();

        Assert.IsTrue(options.ProductAttributeReconciliationEnabled);
        Assert.AreEqual(100, options.ProductAttributeBatchSize);
        Assert.IsTrue(options.AutoCreateProductAttributeValues);
        Assert.AreEqual("grp_46", options.ProductGroupCodes["Kot Ceket"]);
        Assert.AreEqual("grp_47", options.ProductGroupCodes["Eşofman Altı"]);
        Assert.AreEqual("malzeme", options.ProductAttributeTypeCodes["17"]);
        Assert.AreEqual("astar_durumu", options.ProductAttributeTypeCodes["21"]);
        Assert.AreEqual("fermuar", options.ProductAttributeTypeCodes["22"]);
        Assert.AreEqual("esneklik", options.ProductAttributeTypeCodes["23"]);
        CollectionAssert.Contains(options.IgnoredProductAttributeTypeCodes, "30");
    }

    [TestMethod]
    public void EtkinKaynak_GecersizUrunOzelligiBatchBoyutunuReddeder()
    {
        var options = new ErpSourceOptions
        {
            Enabled = true,
            ConnectionString = "Server=example;Database=erp;User Id=reader",
            ProductAttributeBatchSize = 0
        };

        var ex = Assert.ThrowsExactly<InvalidOperationException>(options.Validate);

        StringAssert.Contains(ex.Message, "ProductAttributeBatchSize");
    }

    [TestMethod]
    public void BosUrunGrubuEslesmesiniReddeder()
    {
        var options = new ErpSourceOptions();
        options.ProductGroupCodes["Eşofman Altı"] = " ";

        var ex = Assert.ThrowsExactly<InvalidOperationException>(options.Validate);

        StringAssert.Contains(ex.Message, "product group mapping");
    }
}
