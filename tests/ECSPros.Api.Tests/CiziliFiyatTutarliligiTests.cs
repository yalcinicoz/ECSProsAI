namespace ECSPros.Api.Tests;

/// <summary>
/// M3 (2026-09-09, mobil isteği): liste ile ürün detayının ÇİZİLİ FİYATI aynı kuralı kullanır.
///
/// Kapatılan tutarsızlık: liste, ürünün varyantlarındaki EN YÜKSEK çizili fiyatı gösteriyordu;
/// detay ise yalnız "en ucuz varyantın" çizili fiyatına bakıyordu. Sonuç: o varyantta çizili
/// fiyat yoksa detay indirimi HİÇ göstermiyor (P-00020538, P-00021410), başka varyant seçilince
/// listeden FARKLI değer çıkıyordu (P-00021624: liste 799,99 ↔ detay 599,99).
/// </summary>
[TestClass]
public sealed class CiziliFiyatTutarliligiTests
{
    /// <summary>Ortak kural: aktif varyantların en yüksek pozitif çizili fiyatı, satış fiyatını aşıyorsa.</summary>
    static decimal? Cizili(decimal satisFiyati, params decimal?[] varyantCizilileri)
    {
        var enYuksek = varyantCizilileri.Select(c => c ?? 0m).Where(c => c > 0).DefaultIfEmpty(0m).Max();
        return enYuksek > satisFiyati ? enYuksek : null;
    }

    [TestMethod]
    public void En_ucuz_varyantta_cizili_fiyat_yoksa_bile_indirim_gorunur()
    {
        // P-00020538: 8 varyantın yalnız 2'sinde 799,99 var; satış 299,99.
        var sonuc = Cizili(299.99m, null, null, 799.99m, null, null, null, 799.99m, null);
        Assert.AreEqual(799.99m, sonuc, "Listede görünen indirim detayda da görünmeli.");
    }

    [TestMethod]
    public void Farkli_varyantlarda_farkli_cizili_fiyat_varsa_EN_YUKSEK_secilir()
    {
        // P-00021624: liste 799,99 gösteriyordu, detay 599,99'u seçiyordu.
        Assert.AreEqual(799.99m, Cizili(499.99m, 599.99m, 799.99m));
    }

    [TestMethod]
    public void Cizili_fiyat_satis_fiyatinin_altindaysa_gosterilmez()
    {
        Assert.IsNull(Cizili(499.99m, 399.99m), "Satış fiyatından düşük referans indirim değildir.");
        Assert.IsNull(Cizili(499.99m, null, null));
        Assert.IsNull(Cizili(499.99m, 499.99m), "Eşit fiyat indirim sayılmaz.");
    }

    [TestMethod]
    public void Varyant_kendi_cizili_fiyati_yoksa_urun_referansi_kopyalanir()
    {
        // MK4 kararı: her varyantta compareAtPrice dolu olur — kendi yoksa ürün düzeyi,
        // ama YALNIZ o varyantın satış fiyatından büyükse.
        static decimal? VaryantCizili(decimal? kendi, decimal? urunCizili, decimal varyantSatis)
            => kendi is > 0 ? kendi
             : (urunCizili is { } u && varyantSatis > 0 && u > varyantSatis ? u : null);

        Assert.AreEqual(599.99m, VaryantCizili(599.99m, 799.99m, 499.99m), "Kendi referansı varsa o kullanılır.");
        Assert.AreEqual(799.99m, VaryantCizili(null, 799.99m, 499.99m), "Kendi yoksa ürün referansı kopyalanır.");
        Assert.IsNull(VaryantCizili(null, 799.99m, 899.99m), "Ürün referansı bu varyanttan ucuzsa yazılmaz.");
        Assert.IsNull(VaryantCizili(null, null, 499.99m));
    }
}

/// <summary>
/// M3: varyant seçenek metni tek kuraldan üretilir (<c>VaryantSecenekMetni</c>) — sepet satırı,
/// sipariş/yorum uçları ve ürün detayı aynı metni gösterir.
/// </summary>
[TestClass]
public sealed class VaryantSecenekMetniTests
{
    [TestMethod]
    public void Ic_filtre_ekseni_metne_girmez()
    {
        // Gerçek veri: "Beden: 44, Filtre Rengi: Lacivert, Renk: Lacivert" üretiliyordu —
        // filtre_rengi vitrin filtresi için tutulan iç eksendir, rengi tekrar eder.
        var metin = ECSPros.Shared.Contracts.VaryantSecenekMetni.Kur(
        [
            ("beden", "Beden", "44"),
            ("filtre_rengi", "Filtre Rengi", "Lacivert"),
            ("renk", "Renk", "Lacivert"),
        ]);
        Assert.AreEqual("Renk: Lacivert, Beden: 44", metin);
    }

    [TestMethod]
    public void Sira_renk_sonra_beden_sonra_digerleri()
    {
        var metin = ECSPros.Shared.Contracts.VaryantSecenekMetni.Kur(
        [
            ("kumas", "Kumaş", "Pamuk"),
            ("beden", "Beden", "S"),
            ("renk", "Renk", "Krem"),
        ]);
        Assert.AreEqual("Renk: Krem, Beden: S, Kumaş: Pamuk", metin);
    }

    [TestMethod]
    public void En_cok_uc_secenek_gosterilir()
    {
        var metin = ECSPros.Shared.Contracts.VaryantSecenekMetni.Kur(
        [
            ("renk", "Renk", "Krem"), ("beden", "Beden", "S"),
            ("kumas", "Kumaş", "Pamuk"), ("desen", "Desen", "Düz"),
        ]);
        Assert.AreEqual(3, metin!.Split(", ").Length);
    }

    [TestMethod]
    public void Secenek_yoksa_null_doner()
    {
        Assert.IsNull(ECSPros.Shared.Contracts.VaryantSecenekMetni.Kur([]));
        Assert.IsNull(ECSPros.Shared.Contracts.VaryantSecenekMetni.Kur([("filtre_rengi", "Filtre Rengi", "Mavi")]),
            "Yalnız iç eksen varsa metin üretilmez.");
        Assert.IsNull(ECSPros.Shared.Contracts.VaryantSecenekMetni.Kur([("renk", "Renk", "  ")]),
            "Boş değer metne girmez.");
    }
}
