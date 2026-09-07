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
        Assert.IsTrue(options.SupplierReconciliationEnabled);
        Assert.AreEqual(60, options.SupplierReconciliationMinutes);
        Assert.AreEqual("V3-SUP-696", options.BuildSupplierAccountCode("696"));
        Assert.AreEqual("kot_ceket", options.ProductGroupCodes["Kot Ceket"]);
        Assert.AreEqual("grp_9", options.ProductGroupCodes["Büstiyer"]);
        Assert.IsFalse(options.ProductGroupCodes.ContainsKey("Eşofman Altı"));
        Assert.AreEqual(0, options.ProductGroupPrefixCodes.Count);
        Assert.IsNull(options.ResolveProductGroupPrefixCode("Triko Takım"));
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

    [TestMethod]
    public void BosUrunGrubuPrefixEslesmesiniReddeder()
    {
        var options = new ErpSourceOptions();
        options.ProductGroupPrefixCodes["Triko"] = " ";

        var ex = Assert.ThrowsExactly<InvalidOperationException>(options.Validate);

        StringAssert.Contains(ex.Message, "product group prefix mapping");
    }

    [TestMethod]
    public void AcikcaTanimlananTrikoKuralini_TamKelimeSiniriylaEsler()
    {
        var options = new ErpSourceOptions();
        options.ProductGroupPrefixCodes["Triko"] = "grp_14";
        options.ProductGroupPrefixCodes["Tesettür Triko"] = "grp_14";

        Assert.AreEqual("grp_14", options.ResolveProductGroupPrefixCode("Triko Takım"));
        Assert.AreEqual("grp_14", options.ResolveProductGroupPrefixCode("  TRİKO   Ceket "));
        Assert.AreEqual("grp_14", options.ResolveProductGroupPrefixCode("Triko Bluz"));
        Assert.AreEqual("grp_14", options.ResolveProductGroupPrefixCode("Tesettür Triko Tunik"));
        Assert.AreEqual("grp_14", options.ResolveProductGroupPrefixCode("Tesettür Triko Yelek"));
        Assert.IsNull(options.ResolveProductGroupPrefixCode("Trikolu Bluz"));
        Assert.IsNull(options.ResolveProductGroupPrefixCode("Tesettürlü Triko Tunik"));
        Assert.IsNull(options.ResolveProductGroupPrefixCode("Kot Ceket"));
    }

    [TestMethod]
    public void GecersizTedarikciUzlastirmaAyarlariniReddeder()
    {
        var options = new ErpSourceOptions { SupplierAccountCodePrefix = "", SupplierReconciliationMinutes = 1 };
        var prefixError = Assert.ThrowsExactly<InvalidOperationException>(options.Validate);
        StringAssert.Contains(prefixError.Message, "code prefix");

        options.SupplierAccountCodePrefix = "V3-SUP-";
        var intervalError = Assert.ThrowsExactly<InvalidOperationException>(options.Validate);
        StringAssert.Contains(intervalError.Message, "SupplierReconciliationMinutes");
    }
}
