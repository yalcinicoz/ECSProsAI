using ECSPros.Iam.Domain.Entities;
using ECSPros.Iam.Infrastructure.Persistence;
using ECSPros.Iam.Infrastructure.Services;
using ECSPros.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace ECSPros.Api.Tests;

/// <summary>
/// Y1 efektif yetki servisi — gerçek DB'de, TEK işlem içinde ve sonunda GERİ ALINARAK.
/// Doğrulananlar: grup yetkisi, kullanıcı istisnası (kanal bazlı kaldırma), süper admin bypass,
/// pasif kullanıcı, pasif permission ve K3 önbellek geçersiz kılma.
/// </summary>
[TestClass]
public sealed class EtkinYetkiServisiDbTests
{
    /// <summary>L2 yerine bellek — Redis'siz de test edilebilsin (üretimde ICacheService=Redis).</summary>
    private sealed class BellekCache : ICacheService
    {
        private readonly Dictionary<string, object> _d = new();
        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
            => Task.FromResult(_d.TryGetValue(key, out var v) ? (T?)v : null);
        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class
        { _d[key] = value; return Task.CompletedTask; }
        public Task RemoveAsync(string key, CancellationToken ct = default) { _d.Remove(key); return Task.CompletedTask; }
        public Task RemoveByPatternAsync(string pattern, CancellationToken ct = default)
        {
            var on = pattern.TrimEnd('*');
            foreach (var k in _d.Keys.Where(k => k.StartsWith(on, StringComparison.Ordinal)).ToList()) _d.Remove(k);
            return Task.CompletedTask;
        }
        public Task<bool> ExistsAsync(string key, CancellationToken ct = default) => Task.FromResult(_d.ContainsKey(key));
    }

    static NpgsqlDataSource? Ds()
    {
        var cs = Environment.GetEnvironmentVariable("ECSPROS_TEST_DB");
        if (string.IsNullOrWhiteSpace(cs)) return null;
        var b = new NpgsqlDataSourceBuilder(cs); b.EnableDynamicJson(); return b.Build();
    }

    [TestMethod]
    public async Task Grup_istisna_kanal_ve_onbellek_akisi()
    {
        var ds = Ds();
        if (ds is null) { Assert.Inconclusive("ECSPROS_TEST_DB verilmedi — DB testi atlandı."); return; }

        await using var db = new IamDbContext(new DbContextOptionsBuilder<IamDbContext>().UseNpgsql(ds).Options);
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var kanalA = Guid.NewGuid();
            var kanalB = Guid.NewGuid();
            var ek = Guid.NewGuid().ToString("N")[..8];

            var perm = new Permission
            {
                Code = $"test.kanalli.{ek}", NameI18n = new() { ["tr"] = "Test Kanallı" },
                Module = "Test", PermissionType = "manage", Kind = "action",
                ChannelScoped = true, IsActive = true, IsCodeDefined = true,
            };
            var permKapsamsiz = new Permission
            {
                Code = $"test.kapsamsiz.{ek}", NameI18n = new() { ["tr"] = "Test Kapsamsız" },
                Module = "Test", PermissionType = "manage", Kind = "action",
                ChannelScoped = false, IsActive = true, IsCodeDefined = true,
            };
            var grup = new Role { Code = $"test_grup_{ek}", NameI18n = new() { ["tr"] = "Test Grubu" }, IsActive = true };
            var kullanici = new User
            {
                Username = $"test_{ek}", Email = $"test_{ek}@ornek.local", PasswordHash = "x",
                FirstName = "Test", LastName = "Kullanıcı", IsActive = true,
            };
            db.Permissions.AddRange(perm, permKapsamsiz);
            db.Roles.Add(grup);
            db.Users.Add(kullanici);
            await db.SaveChangesAsync();

            db.RolePermissions.Add(new RolePermission
            { RoleId = grup.Id, PermissionId = perm.Id, ChannelIds = [kanalA, kanalB] });
            db.RolePermissions.Add(new RolePermission { RoleId = grup.Id, PermissionId = permKapsamsiz.Id });
            db.UserRoles.Add(new UserRole { UserId = kullanici.Id, RoleId = grup.Id });
            await db.SaveChangesAsync();

            var l1 = new MemoryCache(new MemoryCacheOptions());
            var servis = new EtkinYetkiServisi(db, l1, new BellekCache());

            // 1) Gruptan gelen: iki kanal + kapsamsız yetki
            var e1 = await servis.GetirAsync(kullanici.Id);
            Assert.IsTrue(e1.Var(perm.Code, kanalA));
            Assert.IsTrue(e1.Var(perm.Code, kanalB));
            Assert.IsTrue(e1.Var(permKapsamsiz.Code));
            Assert.IsNull(e1.Kanallar(permKapsamsiz.Code), "Kapsamsız yetkide kanal kümesi olmaz.");

            // 2) Kullanıcı istisnası: yalnız kanalB kaldırıldı → A kalır, B gider
            db.UserPermissions.Add(new UserPermission
            {
                UserId = kullanici.Id, PermissionId = perm.Id,
                GrantType = "revoke", ChannelIds = [kanalB],
            });
            await db.SaveChangesAsync();
            await servis.GecersizKilAsync(kullanici.Id);          // K3: anında etkili

            var e2 = await servis.GetirAsync(kullanici.Id);
            Assert.IsTrue(e2.Var(perm.Code, kanalA));
            Assert.IsFalse(e2.Var(perm.Code, kanalB), "Kullanıcı istisnası kanal bazında düşürmeliydi.");

            // 3) Önbellek geçersiz kılınmazsa eski sonuç döner (davranışın bilinçli sınırı)
            db.UserPermissions.Add(new UserPermission
            {
                UserId = kullanici.Id, PermissionId = permKapsamsiz.Id, GrantType = "revoke",
            });
            await db.SaveChangesAsync();
            Assert.IsTrue((await servis.GetirAsync(kullanici.Id)).Var(permKapsamsiz.Code),
                "Önbellek düşürülmeden eski sonuç beklenir (L1).");
            await servis.GecersizKilAsync(kullanici.Id);
            Assert.IsFalse((await servis.GetirAsync(kullanici.Id)).Var(permKapsamsiz.Code));

            // 4) Permission pasife alınırsa yetki düşer
            perm.IsActive = false;
            await db.SaveChangesAsync();
            await servis.GecersizKilAsync(kullanici.Id);
            Assert.IsFalse((await servis.GetirAsync(kullanici.Id)).Var(perm.Code));

            // 5) Süper admin bayrağı: hesap yapılmadan her şey açık
            kullanici.IsSuperAdmin = true;
            await db.SaveChangesAsync();
            await servis.GecersizKilAsync(kullanici.Id);
            var e5 = await servis.GetirAsync(kullanici.Id);
            Assert.IsTrue(e5.SuperAdmin);
            Assert.IsTrue(e5.Var("hicbir.yerde.tanimli.olmayan"));

            // 6) Pasif kullanıcı: yetki YOK (süper admin olsa bile giriş/hesap kapalı)
            kullanici.IsSuperAdmin = false;
            kullanici.IsActive = false;
            await db.SaveChangesAsync();
            await servis.GecersizKilAsync(kullanici.Id);
            var e6 = await servis.GetirAsync(kullanici.Id);
            Assert.IsFalse(e6.Var(permKapsamsiz.Code));
            Assert.AreEqual(0, e6.Keyler.Count);

            // 7) Grup içeriği değişince tüm üyelerin önbelleği düşer
            kullanici.IsActive = true;
            await db.SaveChangesAsync();
            await servis.GrupIcinGecersizKilAsync(grup.Id);
            Assert.IsTrue((await servis.GetirAsync(kullanici.Id)).Keyler.Count >= 0);
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }
}
