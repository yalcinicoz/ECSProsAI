using ECSPros.Api.Services.Store;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class UrunGorselSrcsetTests
{
    [TestMethod]
    public void KartSrcset_YuksekDpiAdaylariniIcerir()
    {
        const string url = "https://cdn.misharitalia.com/img/640/85/urun.webp";

        var srcset = UrunGorselSrcset.Kart(url);

        Assert.IsNotNull(srcset);
        CollectionAssert.AreEqual(
            new[] { 240, 360, 480, 640, 768, 1024 },
            UrunGorselSrcset.KartGenislikleri);
        StringAssert.Contains(srcset, "/img/240/85/urun.webp 240w");
        StringAssert.Contains(srcset, "/img/1024/85/urun.webp 1024w");
    }

    [TestMethod]
    public void KartSrcset_CdnDisiAdresiDegistirmez()
    {
        Assert.IsNull(UrunGorselSrcset.Kart("/images/no-image.svg"));
    }
}
