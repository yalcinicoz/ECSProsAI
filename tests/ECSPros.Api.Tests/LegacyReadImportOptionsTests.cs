using ECSPros.Api.Services.LegacyImport;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class LegacyReadImportOptionsTests
{
    [TestMethod]
    public void VarsayilanAyarlar_TumImportuKapaliVeDryRunTutar()
    {
        var options = new LegacyReadImportOptions();

        options.Validate();

        Assert.IsFalse(options.Enabled);
        Assert.IsTrue(options.DryRun);
        Assert.IsTrue(options.ImagesDryRun);
        Assert.AreEqual(0, options.EnabledSlices().Count);
        Assert.AreEqual(41, options.PlatformId);
        Assert.AreEqual(LegacyReturnAmountMismatchPolicies.Block, options.ReturnAmountMismatchPolicy);
        Assert.AreEqual("mishar", options.FirmPlatformCode);
        Assert.AreEqual(2, options.FullReconciliationHourUtc);
        Assert.AreEqual(1440, options.ImagesIntervalMinutes);
        Assert.AreEqual(10, options.MissingImagesIntervalMinutes);
        Assert.AreEqual(25, options.MissingImagesBatchSize);
        Assert.AreEqual(60, options.ImagesFullStartupDelayMinutes);
    }

    [TestMethod]
    public void EtkinImport_GecersizIadeTutarPolitikasiniReddeder()
    {
        var options = new LegacyReadImportOptions
        {
            Enabled = true,
            ReturnsEnabled = true,
            ConnectionString = "Server=example;Database=legacy;User Id=reader",
            ReturnAmountMismatchPolicy = "Guess"
        };

        var ex = Assert.ThrowsExactly<InvalidOperationException>(options.Validate);

        StringAssert.Contains(ex.Message, "ReturnAmountMismatchPolicy");
    }

    [TestMethod]
    public void EtkinIadeImportu_KalemToplamiPolitikasiniKabulEder()
    {
        var options = new LegacyReadImportOptions
        {
            Enabled = true,
            ReturnsEnabled = true,
            ConnectionString = "Server=example;Database=legacy;User Id=reader",
            ReturnAmountMismatchPolicy = LegacyReturnAmountMismatchPolicies.UseItemTotal
        };

        options.Validate();
    }

    [TestMethod]
    public void EtkinImport_BosBaglantiDizesiniReddeder()
    {
        var options = new LegacyReadImportOptions
        {
            Enabled = true,
            MembersEnabled = true
        };

        var ex = Assert.ThrowsExactly<InvalidOperationException>(options.Validate);

        StringAssert.Contains(ex.Message, "ConnectionString");
    }

    [TestMethod]
    public void EtkinImport_DilimSecilmemesiniReddeder()
    {
        var options = new LegacyReadImportOptions
        {
            Enabled = true,
            ConnectionString = "Server=example;Database=legacy;User Id=reader"
        };

        var ex = Assert.ThrowsExactly<InvalidOperationException>(options.Validate);

        StringAssert.Contains(ex.Message, "veri dilimi");
    }

    [TestMethod]
    public void EtkinVeYapilandirilmisUyeDilimi_Gecer()
    {
        var options = new LegacyReadImportOptions
        {
            Enabled = true,
            MembersEnabled = true,
            ConnectionString = "Server=example;Database=legacy;User Id=reader"
        };

        options.Validate();

        CollectionAssert.AreEqual(
            new[] { LegacyImportSlices.Members },
            options.EnabledSlices().ToArray());
    }

    [TestMethod]
    public void EtkinGorselDilimi_AyriKadanslaSecilir()
    {
        var options = new LegacyReadImportOptions
        {
            Enabled = true,
            ImagesEnabled = true,
            ImagesIntervalMinutes = 180,
            ConnectionString = "Server=example;Database=legacy;User Id=reader"
        };

        options.Validate();

        CollectionAssert.AreEqual(
            new[] { LegacyImportSlices.MissingImages, LegacyImportSlices.Images },
            options.EnabledSlices().ToArray());
    }

    [TestMethod]
    public void EtkinImport_GecersizGorselAraliginiReddeder()
    {
        var options = new LegacyReadImportOptions
        {
            Enabled = true,
            ImagesEnabled = true,
            ImagesIntervalMinutes = 10,
            ConnectionString = "Server=example;Database=legacy;User Id=reader"
        };

        var ex = Assert.ThrowsExactly<InvalidOperationException>(options.Validate);

        StringAssert.Contains(ex.Message, "ImagesIntervalMinutes");
    }

    [TestMethod]
    public async Task BosBaglanti_ProbeOncesiAgErisiminiReddeder()
    {
        var source = new MySqlLegacyReadSource(new LegacyReadImportOptions());

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => source.ProbeAsync(41, CancellationToken.None));

        StringAssert.Contains(ex.Message, "ConnectionString");
    }
}
