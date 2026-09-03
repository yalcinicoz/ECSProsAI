using ECSPros.Api.Services.LegacyImport;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class LegacyInvoiceNumberParserTests
{
    [TestMethod]
    [DataRow("MSR2026000000001", "MSR", "2026", 1)]
    [DataRow("TYA2026123456789", "TYA", "2026", 123456789)]
    public void GecerliNumarayiAyristirir(string raw, string serial, string year, int sequence)
    {
        var parsed = LegacyInvoiceNumberParser.Parse(raw);
        Assert.IsNotNull(parsed);
        Assert.AreEqual(serial, parsed.Serial);
        Assert.AreEqual(year, parsed.Year);
        Assert.AreEqual(sequence, parsed.Sequence);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("MSR20261")]
    [DataRow("MS-2026000000001")]
    public void GecersizNumarayiReddeder(string raw) =>
        Assert.IsNull(LegacyInvoiceNumberParser.Parse(raw));
}
