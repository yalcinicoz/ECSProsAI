using System.Security.Claims;
using ECSPros.Api.Authorization;
using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ECSPros.Api.Tests;

/// <summary>
/// Yetki kontrolünün davranışı:
///  • K5 (Y0): süper admin token'daki <c>sa</c> BAYRAĞIYLA geçer; eski ölü <c>permission="*"</c> yok.
///  • K3 (Y1): yetki TOKEN'DAN değil, her istekte <see cref="IEtkinYetkiServisi"/>'nden okunur —
///    token'da permission claim'i olsa bile dikkate alınmaz (kaldırılan yetki hemen kapanır).
///  • Servis yoksa yetki AÇIK varsayılmaz (503) — fail-safe kapalıdır.
/// </summary>
[TestClass]
public sealed class RequirePermissionAttributeTests
{
    private sealed class SahteYetkiServisi(EfektifYetkiler sonuc) : IEtkinYetkiServisi
    {
        public Task<EfektifYetkiler> GetirAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(sonuc);
        public Task GecersizKilAsync(Guid userId, CancellationToken ct = default) => Task.CompletedTask;
        public Task GrupIcinGecersizKilAsync(Guid roleId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static EfektifYetkiler Yetkiler(params string[] keyler)
        => EfektifYetkiHesabi.Hesapla(false,
            keyler.Select(k => new YetkiKaynagi(k, false, null, YetkiKaynakTipi.Grup)));

    private static AuthorizationFilterContext Baglam(EfektifYetkiler? servisSonucu, params Claim[] claims)
    {
        var services = new ServiceCollection();
        if (servisSonucu is not null)
            services.AddSingleton<IEtkinYetkiServisi>(new SahteYetkiServisi(servisSonucu));

        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        if (claims.Length > 0)
            http.User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));

        return new AuthorizationFilterContext(
            new ActionContext(http, new RouteData(), new ActionDescriptor()), []);
    }

    private static Claim Kimlik(Guid id) => new("sub", id.ToString());

    [TestMethod]
    public async Task Kimliksiz_istek_401()
    {
        var ctx = Baglam(Yetkiler());
        await new RequirePermissionAttribute(Permissions.InventoryManage).OnAuthorizationAsync(ctx);
        Assert.IsInstanceOfType(ctx.Result, typeof(UnauthorizedResult));
    }

    [TestMethod]
    public async Task Super_admin_bayragi_tum_kontrolleri_gecer()
    {
        // Servis hiç sorulmaz: bayrak token'dadır.
        var ctx = Baglam(null, Kimlik(Guid.NewGuid()), new Claim("sa", "true"));
        await new RequirePermissionAttribute(Permissions.InventoryManage).OnAuthorizationAsync(ctx);
        Assert.IsNull(ctx.Result);
    }

    [TestMethod]
    public async Task Token_icindeki_permission_claimi_ARTIK_gecerli_degil()
    {
        // Y1 regresyonu: yetki yalnız servisten okunur; eski token'daki claim yetki VERMEZ.
        var ctx = Baglam(Yetkiler(), Kimlik(Guid.NewGuid()),
            new Claim("permission", Permissions.InventoryManage), new Claim("permission", "*"));
        await new RequirePermissionAttribute(Permissions.InventoryManage).OnAuthorizationAsync(ctx);
        Assert.IsInstanceOfType(ctx.Result, typeof(ObjectResult));
        Assert.AreEqual(StatusCodes.Status403Forbidden, ((ObjectResult)ctx.Result!).StatusCode);
    }

    [TestMethod]
    public async Task Servisten_gelen_yetki_gecer_gelmeyen_403()
    {
        var izinli = Baglam(Yetkiler(Permissions.InventoryManage), Kimlik(Guid.NewGuid()));
        await new RequirePermissionAttribute(Permissions.InventoryManage).OnAuthorizationAsync(izinli);
        Assert.IsNull(izinli.Result);

        var izinsiz = Baglam(Yetkiler(Permissions.CatalogProductsManage), Kimlik(Guid.NewGuid()));
        await new RequirePermissionAttribute(Permissions.InventoryManage).OnAuthorizationAsync(izinsiz);
        Assert.AreEqual(StatusCodes.Status403Forbidden, ((ObjectResult)izinsiz.Result!).StatusCode);
    }

    [TestMethod]
    public async Task Yetki_servisi_yoksa_acik_varsayilmaz()
    {
        var ctx = Baglam(null, Kimlik(Guid.NewGuid()));   // servis kayıtlı değil, süper admin de değil
        await new RequirePermissionAttribute(Permissions.InventoryManage).OnAuthorizationAsync(ctx);
        Assert.AreEqual(StatusCodes.Status503ServiceUnavailable, ((ObjectResult)ctx.Result!).StatusCode);
    }
}
