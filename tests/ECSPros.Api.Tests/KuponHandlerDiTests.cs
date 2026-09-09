using ECSPros.Crm.Infrastructure;
using ECSPros.Promotion.Application.Commands.ManageCoupon;
using ECSPros.Promotion.Application.Commands.UseCoupon;
using ECSPros.Promotion.Application.Queries.GetCoupons;
using ECSPros.Promotion.Application.Queries.ValidateCoupon;
using ECSPros.Promotion.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ECSPros.Api.Tests;

/// <summary>
/// Kupon handler'ları (kişiye/gruba özel kupon işi, 2026-09-09) artık CRM'in <c>IMemberService</c>'ini de
/// istiyor. 2026-09-08 canlı olayının dersi: eksik DI kaydı yalnız İSTEK anında 500 olarak görünür.
/// Bu test gerçek modül kayıtlarıyla (DB'ye BAĞLANMADAN) handler'ları kurar.
/// </summary>
[TestClass]
public sealed class KuponHandlerDiTests
{
    [TestMethod]
    public void Kupon_handlerlari_gercek_modul_kayitlariyla_kurulabiliyor()
    {
        // Bağlantı açılmaz; NpgsqlDataSource oluşturmak ağ erişimi gerektirmez.
        var dataSource = new NpgsqlDataSourceBuilder(
            "Host=localhost;Port=5432;Database=yok;Username=yok;Password=yok").Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCrmInfrastructure(dataSource);          // IMemberService buradan gelir
        services.AddPromotionInfrastructure(dataSource);

        using var sp = services.BuildServiceProvider(validateScopes: true);
        using var scope = sp.CreateScope();
        var p = scope.ServiceProvider;

        Assert.IsNotNull(ActivatorUtilities.CreateInstance<ValidateCouponQueryHandler>(p));
        Assert.IsNotNull(ActivatorUtilities.CreateInstance<UseCouponCommandHandler>(p));
        Assert.IsNotNull(ActivatorUtilities.CreateInstance<CreateCouponCommandHandler>(p));
        Assert.IsNotNull(ActivatorUtilities.CreateInstance<UpdateCouponCommandHandler>(p));
        Assert.IsNotNull(ActivatorUtilities.CreateInstance<GetCouponsQueryHandler>(p));
    }
}
