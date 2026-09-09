using System.Reflection;
using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Authorization;

namespace ECSPros.Api.Tests;

/// <summary>
/// Y0 (2026-09-09, karar K4 "kod sahipli katalog"): katalog tutarlılığı. Panelden permission
/// ÜRETİLMEZ; kodda tanımlı olmayan bir key'in panelde görünmesi "hayalet yetki"dir.
/// </summary>
[TestClass]
public sealed class PermissionKataloguTests
{
    [TestMethod]
    public void Keyler_benzersiz_ve_bos_degil()
    {
        var keyler = PermissionKatalogu.Tumu.Select(t => t.Key).ToList();
        CollectionAssert.AllItemsAreUnique(keyler);
        Assert.IsFalse(keyler.Any(string.IsNullOrWhiteSpace));
        Assert.IsFalse(PermissionKatalogu.Tumu.Any(t => string.IsNullOrWhiteSpace(t.Ad)),
            "Her yetkinin panelde görünecek bir adı olmalı.");
        Assert.IsFalse(PermissionKatalogu.Tumu.Any(t => string.IsNullOrWhiteSpace(t.Modul)),
            "Her yetki bir modüle bağlı olmalı (panel gruplaması).");
    }

    [TestMethod]
    public void Permissions_sabitlerinin_tamami_katalogda()
    {
        // Permissions sınıfındaki her string sabit, katalogda bir tanıma sahip olmalı.
        var sabitler = typeof(Permissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        var eksik = sabitler.Where(k => !PermissionKatalogu.Var(k)).ToList();
        Assert.AreEqual(0, eksik.Count,
            "Katalogda tanımı olmayan permission sabitleri: " + string.Join(", ", eksik));
    }

    [TestMethod]
    public void Alan_yetkileri_K6_listesiyle_birebir()
    {
        // K6: v1'de yalnız bu beş hassas alan. Genişletme ayrı karar gerektirir.
        var beklenen = new[]
        {
            Permissions.FieldViewCost, Permissions.FieldViewMargin,
            Permissions.FieldViewCustomerPhone, Permissions.FieldViewCustomerAddress,
            Permissions.FieldViewInternalNotes,
        };
        var alanlar = PermissionKatalogu.AlanYetkileri().Select(t => t.Key).OrderBy(x => x).ToArray();
        CollectionAssert.AreEquivalent(beklenen, alanlar);
    }

    [TestMethod]
    public void Kanal_kapsamli_yetkiler_bilincli_secilmis()
    {
        // K1: kapsam birimi kanal. Bugün yalnız sipariş tarafı kanal bazlıdır; katalog/stok/
        // tanım yetkileri kanaldan bağımsızdır (panelde kanal seçici hiç görünmez).
        var kanalli = PermissionKatalogu.KanalKapsamlilar().Select(t => t.Key).ToArray();
        CollectionAssert.Contains(kanalli, Permissions.OrderPackagesMerge);
        CollectionAssert.DoesNotContain(kanalli, Permissions.DefinitionManage);
        CollectionAssert.DoesNotContain(kanalli, Permissions.CatalogPlatformManage);
    }

    [TestMethod]
    public void Kind_kodlari_dbye_yazilan_degerlerle_uyumlu()
    {
        Assert.AreEqual("page",   PermissionKatalogSenkronu.KindKodu(PermissionTuru.Sayfa));
        Assert.AreEqual("action", PermissionKatalogSenkronu.KindKodu(PermissionTuru.Aksiyon));
        Assert.AreEqual("field",  PermissionKatalogSenkronu.KindKodu(PermissionTuru.Alan));
    }

    [TestMethod]
    public void Yetkilendirme_yetkileri_AllPermissions_listesine_EKLENMEDI()
    {
        // K5 + default deny: yetkilendirme yönetimi ve alan yetkileri hiçbir role otomatik
        // atanmaz; süper admin bayrakla geçer, diğerleri panelden dağıtılır.
        CollectionAssert.DoesNotContain(Permissions.AllPermissions.ToArray(), Permissions.IamPermissionsManage);
        CollectionAssert.DoesNotContain(Permissions.AllPermissions.ToArray(), Permissions.FieldViewCost);
    }
}
