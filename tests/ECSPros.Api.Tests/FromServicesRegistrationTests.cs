using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Tests;

/// <summary>
/// 2026-09-08 canlı olayı: <c>PushEtkilesim</c> controller aksiyonlarında <c>[FromServices]</c> ile istendiği hâlde DI'a kaydedilmemişti;
/// soru cevaplama / yorum onay-ret / iade onay-ret 500 döndü (yetki filtresi model bağlamadan önce çalıştığından kimliksiz duman testi de
/// yakalamaz). Bu test: Api assembly'sindeki tüm controller aksiyonlarının <c>[FromServices]</c> SOMUT sınıf parametreleri için kaynakta
/// bir <c>Add(Scoped|Singleton|Transient)&lt;…Tip&gt;</c> kaydı bulunmasını ister (arayüz/ILogger/IConfiguration vb. framework tipleri hariç).
/// </summary>
[TestClass]
public sealed class FromServicesRegistrationTests
{
    [TestMethod]
    public void Controller_FromServices_somut_tipleri_DI_kaydina_sahip()
    {
        var api = typeof(ECSPros.Api.Grid.GridRequestParser).Assembly;
        var kaynak = string.Join("\n", Directory.EnumerateFiles(RepoDir("src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Select(File.ReadAllText));
        var eksik = new List<string>();
        foreach (var ctrl in api.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract))
        foreach (var m in ctrl.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        foreach (var p in m.GetParameters())
        {
            if (p.GetCustomAttribute<FromServicesAttribute>() is null) continue;
            var t = p.ParameterType;
            if (t.IsInterface || t.IsAbstract || t.IsGenericType || t.Namespace?.StartsWith("Microsoft", StringComparison.Ordinal) == true
                || t.Namespace?.StartsWith("System", StringComparison.Ordinal) == true || t.Namespace?.StartsWith("Npgsql", StringComparison.Ordinal) == true) continue;
            // yalnız gerçek DI çağrısı sayılır: Add(Scoped|Singleton|Transient)<[Arayüz,] Tip>( ya da typeof(Tip) ile; ILogger<Tip> gibi jenerik kullanımlar SAYILMAZ
            var ad = System.Text.RegularExpressions.Regex.Escape(t.Name);
            var kayitli = System.Text.RegularExpressions.Regex.IsMatch(kaynak,
                $@"Add(Scoped|Singleton|Transient)\s*<\s*(?:[A-Za-z0-9_.]+\s*,\s*)?(?:[A-Za-z0-9_]+\.)*{ad}\s*>\s*\(|Add(Scoped|Singleton|Transient)\s*\(\s*typeof\(\s*(?:[A-Za-z0-9_]+\.)*{ad}\s*\)");
            if (!kayitli) eksik.Add($"{ctrl.Name}.{m.Name}({p.Name}: {t.FullName})");
        }
        Assert.AreEqual(0, eksik.Count, "DI kaydı bulunamayan [FromServices] tipleri:\n" + string.Join("\n", eksik));
    }

    private static string RepoDir(string sub)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src"))) dir = dir.Parent;
        Assert.IsNotNull(dir, "Repository root bulunamadı.");
        return Path.Combine(dir!.FullName, sub);
    }
}
