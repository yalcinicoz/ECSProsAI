using System.Reflection;
using ECSPros.Api.Services.Marketplace.Mapping;
using ECSPros.Api.Services.Marketplace.Reference;
using ECSPros.Api.Services.Store;
using ECSPros.Shared.Contracts;
using ECSPros.Storefront.Domain.Entities;
using ECSPros.Storefront.Infrastructure.Migrations;
using ECSPros.Storefront.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class MultiNodeCompatibilityTests
{
    [TestMethod]
    public void ProductQuestion_PendingKurali_ModelVeMigrationTarafindaUnique()
    {
        var options = new DbContextOptionsBuilder<StorefrontDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=model_only;Username=model_only")
            .Options;
        using var db = new StorefrontDbContext(options);

        var entity = db.Model.FindEntityType(typeof(ProductQuestion));
        Assert.IsNotNull(entity);
        var index = entity.GetIndexes().Single(x =>
            x.GetDatabaseName() == "UX_product_questions_single_pending");
        Assert.IsTrue(index.IsUnique);
        StringAssert.Contains(index.GetFilter(), "pending");
        StringAssert.Contains(index.GetFilter(), "IsDeleted");

        var migration = new EnforceSinglePendingProductQuestion();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        typeof(EnforceSinglePendingProductQuestion)
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);

        Assert.IsTrue(builder.Operations.OfType<SqlOperation>()
            .Any(x => x.Sql.Contains("row_number()", StringComparison.OrdinalIgnoreCase)));
        var create = builder.Operations.OfType<CreateIndexOperation>().Single(x =>
            x.Name == "UX_product_questions_single_pending");
        Assert.IsTrue(create.IsUnique);
        StringAssert.Contains(create.Filter, "pending");
    }

    [TestMethod]
    public void NodeYerelFireAndForgetVeMemoryCacheSozlesmeleriKaldirildi()
    {
        var popularCtor = typeof(PopulerAramaServisi).GetConstructors().Single();
        CollectionAssert.Contains(
            popularCtor.GetParameters().Select(x => x.ParameterType).ToArray(),
            typeof(ICacheService));

        var tracker = typeof(AramaTerimIzleyici).GetMethod("KaydetAsync");
        Assert.IsNotNull(tracker);
        Assert.AreEqual(typeof(Task), tracker.ReturnType);
        Assert.IsNull(typeof(AramaTerimIzleyici).GetMethod("Kaydet"));

        var readiness = typeof(MarketplaceMappingService).GetMethod(
            "ReadinessTetikleAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(readiness);
        Assert.AreEqual(typeof(Task), readiness.ReturnType);

        var dailyCheckpoint = typeof(MarketplaceReferenceSyncService)
            .GetMethod("HasCompletedRunOnDayAsync");
        Assert.IsNotNull(dailyCheckpoint);
        Assert.AreEqual(typeof(Task<bool>), dailyCheckpoint.ReturnType);
    }
}
