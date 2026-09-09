using ECSPros.Iam.Application.Services;
using ECSPros.Iam.Domain.Entities;
using ECSPros.Iam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ECSPros.Api.Tests;

/// <summary>
/// Y0 katalog senkronu — gerçek DB'de, TEK işlem içinde ve sonunda GERİ ALINARAK.
/// Doğrulananlar: (1) katalogdaki yetkiler eklenir, (2) panelin yazdığı görünen ad/açıklama/sıra
/// EZİLMEZ, (3) katalogda olmayan kayıt pasife alınır ve "kodda karşılığı yok" işaretlenir,
/// (4) senkron idempotenttir. Yalnız ECSPROS_TEST_DB verildiğinde çalışır.
/// </summary>
[TestClass]
public sealed class PermissionKatalogSenkronuDbTests
{
    static NpgsqlDataSource? Ds()
    {
        var cs = Environment.GetEnvironmentVariable("ECSPROS_TEST_DB");
        if (string.IsNullOrWhiteSpace(cs)) return null;
        var b = new NpgsqlDataSourceBuilder(cs); b.EnableDynamicJson(); return b.Build();
    }

    [TestMethod]
    public async Task Senkron_ekler_panel_alanlarini_korur_ve_yetimleri_pasifler()
    {
        var ds = Ds();
        if (ds is null) { Assert.Inconclusive("ECSPROS_TEST_DB verilmedi — DB testi atlandı."); return; }

        await using var db = new IamDbContext(
            new DbContextOptionsBuilder<IamDbContext>().UseNpgsql(ds).Options);
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // Katalogda OLMAYAN uydurma bir kayıt (eski sürümden kalmış gibi)
            var yetim = new Permission
            {
                Code = "test.orphan." + Guid.NewGuid().ToString("N")[..8],
                NameI18n = new Dictionary<string, string> { ["tr"] = "Yetim Yetki" },
                Module = "Test", PermissionType = "manage", Kind = "action",
                IsActive = true, IsCodeDefined = true,
            };
            db.Permissions.Add(yetim);
            await db.SaveChangesAsync();

            // 1) Senkron
            var s1 = await PermissionKatalogSenkronu.CalistirAsync(db);

            var eklenen = await db.Permissions.FirstOrDefaultAsync(
                p => p.Code == ECSPros.Shared.Kernel.Authorization.Permissions.IamPermissionsManage);
            Assert.IsNotNull(eklenen, "Katalogdaki yeni yetki eklenmeliydi.");
            Assert.AreEqual("page", eklenen!.Kind);
            Assert.IsTrue(eklenen.IsCodeDefined);

            var alan = await db.Permissions.FirstAsync(
                p => p.Code == ECSPros.Shared.Kernel.Authorization.Permissions.FieldViewCost);
            Assert.AreEqual("field", alan.Kind);

            var yetimSonrasi = await db.Permissions.IgnoreQueryFilters()
                .FirstAsync(p => p.Code == yetim.Code);
            Assert.IsFalse(yetimSonrasi.IsCodeDefined, "Katalogda olmayan kayıt işaretlenmeliydi.");
            Assert.IsFalse(yetimSonrasi.IsActive, "Katalogda olmayan kayıt pasife alınmalıydı.");
            Assert.AreEqual(1, s1.Pasiflenen);

            // 2) Panel görünen adı değiştirir → senkron EZMEMELİ
            eklenen.NameI18n = new Dictionary<string, string> { ["tr"] = "PANELDEN DEĞİŞTİ" };
            eklenen.SortOrder = 999;
            await db.SaveChangesAsync();

            var s2 = await PermissionKatalogSenkronu.CalistirAsync(db);
            var tekrar = await db.Permissions.FirstAsync(p => p.Id == eklenen.Id);
            Assert.AreEqual("PANELDEN DEĞİŞTİ", tekrar.NameI18n["tr"]);
            Assert.AreEqual(999, tekrar.SortOrder);
            Assert.AreEqual(0, s2.Eklenen, "İkinci senkron yeni kayıt eklememeli (idempotent).");

            // 3) Kanal-kapsamlı bayrağı: panel değiştirmediyse KOD değeri geçerli,
            //    panel değiştirdiyse (ChannelScopedOverridden) kod bir daha ezmez.
            var kanalli = await db.Permissions.FirstAsync(
                p => p.Code == ECSPros.Shared.Kernel.Authorization.Permissions.OrderPackagesMerge);
            kanalli.ChannelScoped = false; kanalli.ChannelScopedOverridden = false;
            await db.SaveChangesAsync();
            await PermissionKatalogSenkronu.CalistirAsync(db);
            Assert.IsTrue((await db.Permissions.FirstAsync(p => p.Id == kanalli.Id)).ChannelScoped,
                "Panel değiştirmemişken katalog değeri (kanal kapsamlı) yazılmalıydı.");

            kanalli = await db.Permissions.FirstAsync(p => p.Id == kanalli.Id);
            kanalli.ChannelScoped = false; kanalli.ChannelScopedOverridden = true;   // panel kararı
            await db.SaveChangesAsync();
            await PermissionKatalogSenkronu.CalistirAsync(db);
            Assert.IsFalse((await db.Permissions.FirstAsync(p => p.Id == kanalli.Id)).ChannelScoped,
                "Panel kararı senkronla ezilmemeliydi.");
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }
}
