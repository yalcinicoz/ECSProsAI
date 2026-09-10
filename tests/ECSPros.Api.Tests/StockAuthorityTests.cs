using ECSPros.Api.Services.Inventory;
using ECSPros.Shared.Contracts;
using Microsoft.Extensions.Configuration;

namespace ECSPros.Api.Tests;

/// <summary>FAZ 15.3 raf ekranları: stok otoritesi kipi (kurgu §0) — canlı Legacy:Sync stok dilimi açıkken raf yazma uçları kapalı.</summary>
[TestClass]
public sealed class StockAuthorityTests
{
    private static StockAuthority Ayar(params (string k, string v)[] kv)
        => new(new ConfigurationBuilder().AddInMemoryCollection(kv.Select(x => new KeyValuePair<string, string?>(x.k, x.v))).Build());

    [TestMethod]
    public void Legacy_sync_stok_dilimi_acikken_otorite_eskide()
    {
        var a = Ayar(("Legacy:Sync:Enabled", "true"), ("Legacy:Sync:DryRun", "false"));
        Assert.IsTrue(a.LegacyOwnsStock);   // Legacy:Sync:Stock varsayılanı true
        Assert.AreEqual(StockAuthority.Legacy, a.Current);
    }

    [TestMethod]
    public void Stok_dilimi_kapaliysa_veya_sync_kapaliysa_otorite_panel()
    {
        Assert.AreEqual(StockAuthority.Panel, Ayar(("Legacy:Sync:Enabled", "true"), ("Legacy:Sync:Stock", "false")).Current);
        Assert.AreEqual(StockAuthority.Panel, Ayar(("Legacy:Sync:Enabled", "false")).Current);
        Assert.AreEqual(StockAuthority.Panel, Ayar().Current);
        Assert.AreEqual(StockAuthority.Panel, Ayar(("Legacy:Sync:Enabled", "true"), ("Legacy:Sync:DryRun", "true")).Current);   // kuru çalışma yazmaz
    }

    [TestMethod]
    public void Sayim_durumu_sozlugu_panelde()
    {
        Assert.AreEqual("Uygulandı", DurumEtiketleri.Etiket(DurumEtiketleri.Panel.SayimDurumu, "applied"));
        Assert.AreEqual("Sayılıyor", DurumEtiketleri.Etiket(DurumEtiketleri.Panel.SayimDurumu, "open"));
    }
}
