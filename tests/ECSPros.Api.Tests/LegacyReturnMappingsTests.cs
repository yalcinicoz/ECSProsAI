using ECSPros.Api.Services.LegacyImport;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class LegacyReturnMappingsTests
{
    [TestMethod]
    public void BilinenNedenleriSabitKodlaraEsler()
    {
        Assert.AreEqual("legacy_unspecified", LegacyReturnMappings.ReasonCode(1));
        Assert.AreEqual("legacy_disliked", LegacyReturnMappings.ReasonCode(2));
        Assert.AreEqual("legacy_size", LegacyReturnMappings.ReasonCode(3));
        Assert.AreEqual("legacy_not_delivered", LegacyReturnMappings.ReasonCode(9));
        Assert.AreEqual("legacy_unknown", LegacyReturnMappings.ReasonCode(999));
    }

    [TestMethod]
    public void AnlamiOnaylanmamisKodlariTahminEtmedenKorur()
    {
        Assert.AreEqual("legacy_type_1", LegacyReturnMappings.ReturnType(1));
        Assert.AreEqual("legacy_type_2", LegacyReturnMappings.ReturnType(2));
        Assert.AreEqual("legacy_type_1", LegacyReturnMappings.RefundMethod(1));
    }
}
