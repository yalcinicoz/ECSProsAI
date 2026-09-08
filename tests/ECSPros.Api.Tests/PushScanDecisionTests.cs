using ECSPros.Api.Services.Push;
using ECSPros.Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ECSPros.Api.Tests;

/// <summary>
/// Push tarama karar kuralları (2026-09-08, favorite_back_in_stock + cart_price_drop): stok geçişi, fiyat düşüşü eşiği ve
/// favorinin renk kırılımı. Tarama adımlarının geri kalanı DB/katalog servisleri ister; karar mantığı burada saf test edilir.
/// </summary>
[TestClass]
public sealed class PushScanDecisionTests
{
    [TestMethod]
    public void Stok_gecisi_yalnizca_yoktan_vara_donuste_bildirir()
    {
        Assert.AreEqual((false, true), PushTarayici.StokGecisi(oncekiStokYok: false, mevcut: 0), "ilk kez tükendi: işaretlenir, bildirim yok");
        Assert.AreEqual((false, true), PushTarayici.StokGecisi(true, 0), "hâlâ yok");
        Assert.AreEqual((false, true), PushTarayici.StokGecisi(false, -2), "negatif stok da yok sayılır");
        Assert.AreEqual((true, false), PushTarayici.StokGecisi(true, 5), "stok döndü → bildir + işareti kaldır");
        Assert.AreEqual((false, false), PushTarayici.StokGecisi(false, 5), "hiç tükenmemişti → bildirim yok");
    }

    [TestMethod]
    public void Fiyat_dususu_esigi_tam_esikte_gecerli_artista_ve_sifir_tabanda_gecersiz()
    {
        const decimal esik = 0.10m;
        Assert.IsTrue(PushTarayici.FiyatDususuVarMi(100m, 90m, esik), "tam eşik (%10) dahil");
        Assert.IsTrue(PushTarayici.FiyatDususuVarMi(100m, 45m, esik));
        Assert.IsFalse(PushTarayici.FiyatDususuVarMi(100m, 91m, esik), "eşiğin altı");
        Assert.IsFalse(PushTarayici.FiyatDususuVarMi(100m, 100m, esik), "değişmedi");
        Assert.IsFalse(PushTarayici.FiyatDususuVarMi(100m, 120m, esik), "arttı");
        Assert.IsFalse(PushTarayici.FiyatDususuVarMi(0m, 50m, esik), "taban yok");
        Assert.IsFalse(PushTarayici.FiyatDususuVarMi(100m, 0m, esik), "güncel fiyat yok (fiyatsız ürün)");
    }

    [TestMethod]
    public void Favori_renk_kirilimi_eslesme_yoksa_urun_geneline_duser()
    {
        var kirmizi = Guid.NewGuid(); var mavi = Guid.NewGuid();
        var v1 = Guid.NewGuid(); var v2 = Guid.NewGuid(); var v3 = Guid.NewGuid();
        var varyantlar = new List<(Guid, Guid?)> { (v1, kirmizi), (v2, kirmizi), (v3, mavi) };

        CollectionAssert.AreEquivalent(new[] { v1, v2 }, PushTarayici.RenkVaryantlari(varyantlar, kirmizi), "favorilenen rengin varyantları");
        CollectionAssert.AreEquivalent(new[] { v3 }, PushTarayici.RenkVaryantlari(varyantlar, mavi));
        CollectionAssert.AreEquivalent(new[] { v1, v2, v3 }, PushTarayici.RenkVaryantlari(varyantlar, null), "renksiz favori → ürünün tümü");
        CollectionAssert.AreEquivalent(new[] { v1, v2, v3 }, PushTarayici.RenkVaryantlari(varyantlar, Guid.NewGuid()), "renk artık yok → ürünün tümü (yedek)");
    }

    /// <summary>Yeni taramaların sorguları gerçek şemada çalışır mı (SALT OKUNUR): favori renk SQL'i + sepet kalemi EF birleşimi.</summary>
    [TestMethod]
    public async Task Yeni_tarama_sorgulari_gercek_semada_calisir()
    {
        var cs = Environment.GetEnvironmentVariable("ECSPROS_TEST_DB");
        if (string.IsNullOrWhiteSpace(cs)) { Assert.Inconclusive("ECSPROS_TEST_DB verilmedi — DB testi atlandı."); return; }
        var b = new NpgsqlDataSourceBuilder(cs); b.EnableDynamicJson();
        await using var ds = b.Build();

        // favorite_back_in_stock: ürün kodu → varyant + renk ekseni
        await using (var c = await ds.OpenConnectionAsync())
        await using (var cmd = new NpgsqlCommand(PushTarayici.VaryantRenkSql, c))
        {
            var kodlar = new List<string>();
            await using (var kod = new NpgsqlCommand("SELECT \"Code\" FROM catalog.products WHERE NOT \"IsDeleted\" LIMIT 5", c))
            await using (var r0 = await kod.ExecuteReaderAsync())
                while (await r0.ReadAsync()) kodlar.Add(r0.GetString(0));
            cmd.Parameters.AddWithValue("c", kodlar.ToArray());
            int satir = 0;
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) { _ = r.GetGuid(2); _ = r.IsDBNull(3) ? (Guid?)null : r.GetGuid(3); satir++; }
            Assert.IsTrue(kodlar.Count == 0 || satir >= 0, "renk sorgusu çalıştı");
        }

        // cart_price_drop: üye sepetlerinin kalemleri (yazma yok)
        await using var cdb = new CrmDbContext(new DbContextOptionsBuilder<CrmDbContext>().UseNpgsql(ds).Options);
        var sinir = DateTime.UtcNow.AddDays(-14);
        var kalemler = await (from i in cdb.CartItems.AsNoTracking()
                              join k in cdb.Carts.AsNoTracking() on i.CartId equals k.Id
                              where k.MemberId != null && (k.UpdatedAt ?? k.CreatedAt) >= sinir
                              select new { i.VariantId, i.EffectivePriceAtAdd, k.MemberId, k.FirmPlatformId }).Take(50).ToListAsync();
        Assert.IsTrue(kalemler.Count >= 0, "sepet kalemi sorgusu SQL'e çevrildi");
    }
}
