using System.Reflection;
using ECSPros.Shared.Kernel.Grid;

namespace ECSPros.Api.Tests;

/// <summary>
/// Y3 (2026-09-09, karar K2): "kullanıcının erişemediği kanalın verisi hiçbir yüzeyden sızmamalı".
/// Bu test, kanal kolonu OLAN bir listenin kapsam bildirimini (Kanal(...)) unutmasını engeller:
/// yeni bir grid şeması eklenip kanal bildirimi yapılmazsa test kırılır.
/// </summary>
[TestClass]
public sealed class KanalKapsamiTests
{
    /// <summary>Kanal kolonu olduğu hâlde HENÜZ kapsanmayan listeler — her biri gerekçeli ve takipli.</summary>
    private static readonly Dictionary<string, string> Bekleyenler = new()
    {
        // (bu listeye eklenen her satır Y3'ün kalan işidir)
    };

    private static IEnumerable<(string Ad, Type Entity, object Schema)> Semalar()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.FullName?.StartsWith("ECSPros.", StringComparison.Ordinal) == true);

        foreach (var asm in assemblies)
        foreach (var t in asm.GetTypes())
        foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (!f.FieldType.IsGenericType || f.FieldType.GetGenericTypeDefinition() != typeof(GridSchema<>)) continue;
            var deger = f.GetValue(null);
            if (deger is null) continue;
            yield return ($"{t.Name}.{f.Name}", f.FieldType.GetGenericArguments()[0], deger);
        }
    }

    [TestMethod]
    public void Kanal_kolonu_olan_her_grid_kapsam_bildirir()
    {
        // Şemaların yüklendiğinden emin ol (tembel yükleme)
        _ = ECSPros.Order.Application.Queries.GetOrders.OrderGrid.Schema;
        _ = ECSPros.Order.Application.Queries.GetQuotes.QuoteGrid.Schema;
        _ = ECSPros.Promotion.Application.Queries.GetCampaigns.CampaignGrid.Schema;
        _ = ECSPros.Crm.Application.Tickets.Queries.TicketGrid.Schema;

        var eksik = new List<string>();
        var bulundu = 0;

        foreach (var (ad, entity, schema) in Semalar())
        {
            var kanalKolonu = entity.GetProperty("FirmPlatformId") is not null;
            if (!kanalKolonu) continue;
            bulundu++;

            var kapsamli = (bool)schema.GetType().GetProperty("KanalKapsamli")!.GetValue(schema)!;
            if (!kapsamli && !Bekleyenler.ContainsKey(ad)) eksik.Add(ad);
        }

        Assert.IsTrue(bulundu >= 4, $"Kanal kolonlu grid şeması bulunamadı (yüklenen: {bulundu}).");
        Assert.AreEqual(0, eksik.Count,
            "Kanal kolonu olduğu hâlde kapsam bildirmeyen grid şeması (Kanal(...) ekleyin ya da " +
            "gerekçesiyle Bekleyenler listesine alın):\n" + string.Join("\n", eksik));
    }

    [TestMethod]
    public void Kapsam_kisiti_bos_kume_ise_hicbir_satir_donmez()
    {
        var veri = new[]
        {
            new Ornek(Guid.NewGuid()), new Ornek(Guid.NewGuid()),
        }.AsQueryable();
        var sema = new GridSchema<Ornek>().Kanal(o => o.FirmPlatformId);

        // null → kısıt yok (süper admin / kapsamsız yetki)
        Assert.AreEqual(2, sema.ApplyKanalKapsami(veri, null).Count());
        // boş küme → default deny
        Assert.AreEqual(0, sema.ApplyKanalKapsami(veri, Array.Empty<Guid>()).Count());
        // tek kanal → yalnız o kanal
        Assert.AreEqual(1, sema.ApplyKanalKapsami(veri, new[] { veri.First().FirmPlatformId }).Count());
    }

    [TestMethod]
    public void Kapsam_bildirmeyen_semada_kisit_istenirse_hata()
    {
        var sema = new GridSchema<Ornek>();
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            sema.ApplyKanalKapsami(Array.Empty<Ornek>().AsQueryable(), new[] { Guid.NewGuid() }).ToList());
    }


    [TestMethod]
    public async Task Kanal_parametreli_uc_kapsam_disinda_404_doner()
    {
        // Y3 3. tur: vitrin/menü/CMS gibi kanal PARAMETRELİ uçlarda liste filtresi yoktur;
        // filtre argümandaki kanalı denetler ve kapsam dışındaysa 404 verir (403 değil).
        var izinli = Guid.NewGuid();
        var yasak = Guid.NewGuid();

        var ctx = EylemBaglami(new SahteKapsam(new[] { izinli }), ("firmPlatformId", yasak));
        var filtre = new ECSPros.Api.Authorization.KanalKapsamiKontrolAttribute(
            ECSPros.Shared.Kernel.Authorization.Permissions.StorefrontContentView);
        var calisti = false;
        await filtre.OnActionExecutionAsync(ctx, () => { calisti = true; return Task.FromResult(SonucBaglami(ctx)); });

        Assert.IsFalse(calisti, "Kapsam dışı kanalda aksiyon çalışmamalıydı.");
        Assert.IsInstanceOfType(ctx.Result, typeof(Microsoft.AspNetCore.Mvc.NotFoundObjectResult));

        // izinli kanalda geçer
        var ctx2 = EylemBaglami(new SahteKapsam(new[] { izinli }), ("firmPlatformId", izinli));
        var calisti2 = false;
        await filtre.OnActionExecutionAsync(ctx2, () => { calisti2 = true; return Task.FromResult(SonucBaglami(ctx2)); });
        Assert.IsTrue(calisti2);
        Assert.IsNull(ctx2.Result);

        // kısıtsız (süper admin) geçer
        var ctx3 = EylemBaglami(new SahteKapsam(null), ("firmPlatformId", yasak));
        var calisti3 = false;
        await filtre.OnActionExecutionAsync(ctx3, () => { calisti3 = true; return Task.FromResult(SonucBaglami(ctx3)); });
        Assert.IsTrue(calisti3);
    }

    private sealed class SahteKapsam(IReadOnlyCollection<Guid>? kanallar) : ECSPros.Api.Authorization.IKanalKapsami
    {
        public Task<IReadOnlyCollection<Guid>?> KanallarAsync(string permissionKey, CancellationToken ct = default)
            => Task.FromResult(kanallar);
        public Task<bool> ErisebilirMiAsync(string permissionKey, Guid kanalId, CancellationToken ct = default)
            => Task.FromResult(kanallar is null || kanallar.Contains(kanalId));
    }

    private static Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext EylemBaglami(
        ECSPros.Api.Authorization.IKanalKapsami kapsam, params (string Ad, object Deger)[] argumanlar)
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions
            .AddSingleton(services, kapsam);
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext
        {
            RequestServices = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions
                .BuildServiceProvider(services),
        };
        var actionContext = new Microsoft.AspNetCore.Mvc.ActionContext(http,
            new Microsoft.AspNetCore.Routing.RouteData(),
            new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        var args = new Dictionary<string, object?>();
        foreach (var (ad, deger) in argumanlar) args[ad] = deger;
        return new Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext(
            actionContext, new List<Microsoft.AspNetCore.Mvc.Filters.IFilterMetadata>(), args, controller: new object());
    }

    private static Microsoft.AspNetCore.Mvc.Filters.ActionExecutedContext SonucBaglami(
        Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext ctx)
        => new(ctx, new List<Microsoft.AspNetCore.Mvc.Filters.IFilterMetadata>(), controller: new object());

    private sealed record Ornek(Guid FirmPlatformId);
}
