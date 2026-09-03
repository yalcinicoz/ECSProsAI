using ECSPros.Api.Services.LegacyImport;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class LegacyOrderStatusMapperTests
{
    [TestMethod]
    [DataRow("Faturası Kesildi", "processing")]
    [DataRow("İptal Edildi", "cancelled")]
    [DataRow("Teslim Edildi", "delivered")]
    [DataRow("Teslim Edilemeden İade Geldi", "returned")]
    [DataRow("Teslim Edilmeden İade", "returned")]
    public void BilinenLegacyDurumlari_TekSozlugeDonusturur(string source, string expected)
    {
        var result = LegacyOrderStatusMapper.Map(source);

        Assert.IsNotNull(result);
        Assert.AreEqual(expected, result.Status);
    }

    [TestMethod]
    public void BilinmeyenDurum_TahminiConfirmedYapilmaz()
    {
        Assert.IsNull(LegacyOrderStatusMapper.Map("Yeni ve bilinmeyen durum"));
    }
}
