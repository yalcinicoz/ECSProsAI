namespace ECSPros.Api.Tests;

/// <summary>SQL kapsam kapılarının kaynak sözleşmesi; gerçek DB kabulünün yerine geçmez.</summary>
[TestClass]
public sealed class LegacyInvoiceScopeContractTests
{
    [TestMethod]
    public void FaturaOkuma_TekKaynakPlatformlaSinirli()
    {
        var code = Source("LegacyInvoiceReader.cs");
        StringAssert.Contains(code, "WHERE platformId = @platformId");
        StringAssert.Contains(code, "AddWithValue(\"@platformId\", platformId)");
    }

    [TestMethod]
    public void HedefSiparisKanallaSinirli_TarihselSeriGuncelFirmayaZorlanmaz()
    {
        var code = Source("LegacyInvoiceImportSlice.cs");
        StringAssert.Contains(code, "options.FirmPlatformCode");
        StringAssert.Contains(code, "Hedef firma platformu tekil değil");
        StringAssert.Contains(code, "AND \"FirmPlatformId\" = @platformId");
        StringAssert.Contains(code, "AddWithValue(\"platformId\", platformId)");
        Assert.IsFalse(code.Contains("AND \"FirmId\" = @firmId", StringComparison.Ordinal));
        StringAssert.Contains(code, "matches.Length == 1");
        StringAssert.Contains(code, "tarihsel firma doğrulanmalı");
        StringAssert.Contains(code, "SELECT \"Id\",\"Serial\",\"InvoiceType\"");
        StringAssert.Contains(code, "x.InvoiceType == invoiceType");
        Assert.IsFalse(code.Contains("EArchiveSerial", StringComparison.Ordinal));
    }

    private static string Source(string name)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "src", "ECSPros.Api", "Services", "LegacyImport", name);
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException("Repository kaynağı bulunamadı", name);
    }
}
