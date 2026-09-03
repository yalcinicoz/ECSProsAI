namespace ECSPros.Api.Tests;

[TestClass]
public sealed class ErpVariantPriceInheritanceTests
{
    [TestMethod]
    public void ErpYeniVaryanti_AnaUrunFiyatVeMaliyetiniDevralir()
    {
        var service = File.ReadAllText(RepoFile(
            "src", "ECSPros.Api", "Services", "ErpSource", "ErpSourceSyncService.cs"));

        var methodStart = service.IndexOf(
            "private static async Task<(Guid? Id, bool Changed)> UpsertVariantAsync",
            StringComparison.Ordinal);
        var methodEnd = service.IndexOf(
            "private async Task<AttributeReplaceResult> ReplaceVariantAttributesAsync",
            methodStart,
            StringComparison.Ordinal);

        Assert.IsTrue(methodStart >= 0 && methodEnd > methodStart, "UpsertVariantAsync metodu bulunamadı.");
        var method = service[methodStart..methodEnd];

        StringAssert.Contains(method, "\"BasePrice\",\"BaseCost\"");
        StringAssert.Contains(method, "p.\"BasePrice\",p.\"BaseCost\"");
        StringAssert.Contains(method, "FROM catalog.products p");
        StringAssert.Contains(method, "p.\"Id\"=@product AND NOT p.\"IsDeleted\"");
        Assert.IsFalse(method.Contains("@barcode,0,true", StringComparison.Ordinal),
            "ERP varyantı sabit sıfır fiyatla oluşturulmamalıdır.");
    }

    private static string RepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
            directory = directory.Parent;

        Assert.IsNotNull(directory, "Repository root bulunamadı.");
        return Path.Combine([directory.FullName, .. parts]);
    }
}
