using ECSPros.Shared.Kernel.Authorization;

namespace ECSPros.Api.Tests;

/// <summary>
/// Y8 (2026-09-09): departman grubu şablonları K8 adım 3'ün aracıdır. Şablon yanlışsa
/// "geçici tam erişim"i yeni bir kalıcı tam erişimle değiştirmiş oluruz — bu testler
/// şablonların o çizgiyi geçmesini engeller.
/// </summary>
[TestClass]
public sealed class DepartmanGrupSablonlariTests
{
    static readonly HashSet<string> KatalogKeyleri =
        PermissionKatalogu.Tumu.Select(p => p.Key).ToHashSet(StringComparer.Ordinal);

    [TestMethod]
    public void Sablonlardaki_her_key_katalogda_vardir()
    {
        foreach (var s in DepartmanGrupSablonlari.Tumu)
            foreach (var key in s.Yetkiler)
                Assert.IsTrue(KatalogKeyleri.Contains(key),
                    $"'{s.Kod}' şablonundaki '{key}' katalogda yok — hayalet yetki kurar.");
    }

    [TestMethod]
    public void Hicbir_sablon_yonetim_yetkisi_dagitmaz()
    {
        // Y2 kararı: iam.*, definition.manage, system.migration.manage ve credential açığa çıkarma
        // YALNIZ süper adminde kalır; bir departman şablonuyla dolaylı olarak dağıtılamaz.
        string[] yasak =
        [
            Permissions.IamPermissionsManage, Permissions.IamPermissionsSimulate,
            Permissions.IamAuditView, Permissions.IamUsersView, Permissions.IamUsersManage,
            Permissions.DefinitionManage, Permissions.SystemMigrationManage,
            Permissions.IntegrationCredentialsReveal, Permissions.CatalogPlatformManage,
        ];

        foreach (var s in DepartmanGrupSablonlari.Tumu)
            foreach (var key in yasak)
                Assert.IsFalse(s.Yetkiler.Contains(key),
                    $"'{s.Kod}' şablonu yönetim yetkisi '{key}' dağıtıyor.");
    }

    [TestMethod]
    public void Hicbir_sablon_TUM_katalogu_vermez()
    {
        // Şablon "geçiş grubunun kopyası" olmamalı: en geniş şablon bile tüm yetkileri taşımaz.
        foreach (var s in DepartmanGrupSablonlari.Tumu)
            Assert.IsTrue(s.Yetkiler.Count < KatalogKeyleri.Count,
                $"'{s.Kod}' şablonu katalogun tamamını veriyor — geçici tam erişimi kalıcılaştırır.");
    }

    [TestMethod]
    public void Salt_okunur_sablon_hicbir_yazma_yetkisi_icermez()
    {
        var raporlama = DepartmanGrupSablonlari.Bul("raporlama");
        Assert.IsNotNull(raporlama);
        foreach (var key in raporlama!.Yetkiler)
            Assert.IsFalse(key.EndsWith(".manage", StringComparison.Ordinal),
                $"Salt okunur şablonda yazma yetkisi var: {key}");
    }

    [TestMethod]
    public void Salt_okunur_sablon_hassas_alan_acmaz()
    {
        var raporlama = DepartmanGrupSablonlari.Bul("raporlama")!;
        var alanlar = PermissionKatalogu.Tumu.Where(p => p.Tur == PermissionTuru.Alan).Select(p => p.Key);
        foreach (var alan in alanlar)
            Assert.IsFalse(raporlama.Yetkiler.Contains(alan),
                $"Salt okunur şablon hassas alan açıyor: {alan}");
    }

    [TestMethod]
    public void Depo_sablonu_maliyet_gormez_musteri_iliskileri_maliyet_gormez()
    {
        // K6'nın iki tipik sınırı şablonlara da yansımalı.
        Assert.IsFalse(DepartmanGrupSablonlari.Bul("depo_operasyon")!.Yetkiler.Contains(Permissions.FieldViewCost));
        Assert.IsFalse(DepartmanGrupSablonlari.Bul("musteri_iliskileri")!.Yetkiler.Contains(Permissions.FieldViewCost));
        // Satın alma ve muhasebe maliyeti GÖRÜR (işlerinin konusu).
        Assert.IsTrue(DepartmanGrupSablonlari.Bul("satin_alma")!.Yetkiler.Contains(Permissions.FieldViewCost));
        Assert.IsTrue(DepartmanGrupSablonlari.Bul("muhasebe_finans")!.Yetkiler.Contains(Permissions.FieldViewCost));
    }

    [TestMethod]
    public void Kodlar_benzersiz_ve_grup_kodu_bicimindedir()
    {
        var kodlar = DepartmanGrupSablonlari.Tumu.Select(s => s.Kod).ToList();
        CollectionAssert.AllItemsAreUnique(kodlar);
        foreach (var kod in kodlar)
            Assert.IsTrue(kod.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'),
                $"Şablon kodu grup kodu biçiminde değil: {kod}");
    }
}
