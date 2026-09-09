using System.Reflection;
using ECSPros.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using ECSPros.Shared.Kernel.Authorization;

namespace ECSPros.Api.Tests;

/// <summary>
/// Y2 (2026-09-09) — UÇ KAPLAMASI. Tasarım §M.3: "permission'a bağlanmamış uç, yetkilendirme
/// devreye girdiğinde KAPALI sayılır"; pratikte bunu sağlamanın yolu, kaplanmamış ucun derleme/test
/// aşamasında GÖRÜNMESİDİR. Bu test panel (iç) yüzeyindeki her aksiyonun bir yetkiye bağlı
/// olmasını ister; bilinçli istisnalar aşağıda GEREKÇESİYLE listelenir.
///
/// Kapsam dışı yüzeyler (kendi kimlik/yetki dünyaları var): Store* (üye/vitrin), Supplier* (satıcı
/// paneli), Partner* (dış API), Feed (anonim besleme).
/// </summary>
[TestClass]
public sealed class YetkiKaplamaTests
{
    /// <summary>Yetki aranmayan panel uçları — her biri GEREKÇELİ.</summary>
    private static readonly Dictionary<string, string> Istisnalar = new()
    {
        ["AuthController.Login"]           = "Kimlik doğrulama — yetki öncesi.",
        ["AuthController.Refresh"]         = "Token yenileme — yetki öncesi.",
        ["AuthController.ApiToken"]        = "API istemci token'ı — ayrı kimlik dünyası (ApiScopes).",
        ["AuthController.Me"]              = "Kullanıcının kendi kimliği/efektif yetkisi.",
        ["AuthController.ChangePassword"]  = "Kullanıcının kendi şifresi.",
        ["UsersController.GetMyPreferences"] = "Kullanıcının kendi panel tercihleri.",
        ["UsersController.SaveMyPreferences"] = "Kullanıcının kendi panel tercihleri.",
    };

    private static bool PanelYuzeyi(Type t)
    {
        var ad = t.Name;
        if (ad.StartsWith("Store", StringComparison.Ordinal)) return false;
        if (ad.StartsWith("Supplier", StringComparison.Ordinal)) return false;
        if (ad.StartsWith("Partner", StringComparison.Ordinal)) return false;
        if (ad is "FeedController") return false;
        // Views/SSR controller'ları (Store klasörü) — namespace'ten ayırt edilir
        return t.Namespace?.Contains(".Store", StringComparison.Ordinal) != true;
    }

    [TestMethod]
    public void Panel_uclarinin_tamami_bir_yetkiye_bagli()
    {
        var api = typeof(ECSPros.Api.Grid.GridRequestParser).Assembly;
        var kapsanmamis = new List<string>();

        foreach (var ctrl in api.GetTypes()
                     .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
                     .Where(PanelYuzeyi)
                     .OrderBy(t => t.Name))
        {
            var sinifYetkisi = ctrl.GetCustomAttributes<RequirePermissionAttribute>(inherit: true).Any();

            foreach (var m in ctrl.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (m.GetCustomAttributes<HttpMethodAttribute>(inherit: true).FirstOrDefault() is null) continue;
                if (m.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null) continue;

                var ad = $"{ctrl.Name}.{m.Name}";
                if (Istisnalar.ContainsKey(ad)) continue;
                if (sinifYetkisi || m.GetCustomAttributes<RequirePermissionAttribute>(inherit: true).Any()) continue;

                kapsanmamis.Add(ad);
            }
        }

        Assert.AreEqual(0, kapsanmamis.Count,
            "Yetkiye bağlanmamış panel ucu (ya [RequirePermission] ekleyin ya da gerekçesiyle " +
            "Istisnalar listesine alın):\n" + string.Join("\n", kapsanmamis));
    }

    [TestMethod]
    public void Kullanilan_tum_yetki_keyleri_katalogda_tanimli()
    {
        // Hayalet yetki koruması: attribute'ta yazılı ama katalogda olmayan key, kimseye
        // verilemeyeceği için ucu kalıcı olarak kilitler.
        var api = typeof(ECSPros.Api.Grid.GridRequestParser).Assembly;
        var eksik = new List<string>();

        foreach (var ctrl in api.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract))
        {
            foreach (var a in ctrl.GetCustomAttributes<RequirePermissionAttribute>(inherit: true))
                if (!PermissionKatalogu.Var(a.Permission)) eksik.Add($"{ctrl.Name} → {a.Permission}");

            foreach (var m in ctrl.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                foreach (var a in m.GetCustomAttributes<RequirePermissionAttribute>(inherit: true))
                    if (!PermissionKatalogu.Var(a.Permission)) eksik.Add($"{ctrl.Name}.{m.Name} → {a.Permission}");
        }

        Assert.AreEqual(0, eksik.Count, "Katalogda olmayan yetki key'i kullanılıyor:\n" + string.Join("\n", eksik));
    }
}
