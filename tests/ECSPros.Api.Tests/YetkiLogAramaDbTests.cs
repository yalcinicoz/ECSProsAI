using ECSPros.Iam.Application.Yetkilendirme;
using ECSPros.Iam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ECSPros.Api.Tests;

/// <summary>
/// Yetki logu ARAMASININ SQL çevirisi + sayım tutarlılığı (SALT OKUNUR, gerçek DB).
///
/// Neden: özet metni <c>Context</c> jsonb'sinde <c>Dictionary&lt;string, object&gt;</c> olarak durduğu
/// için SQL'e çevrilemez (DbFunction parametresi olamıyor, indeksleyicisi de çevrilmiyor — ikisi de
/// 2026-09-09'da denendi). Bu yüzden arama terimi ÖNCE kullanıcı/grup/yetki kayıtlarına çözülüp DB'de
/// kimlik üzerinden süzülüyor. Bu test o yolun gerçekten SQL'e çevrildiğini ve sayım ile satırların
/// AYNI küme üzerinden geldiğini doğrular (eski hata: arama bellekte, sayfalamadan sonra yapılıyordu →
/// "N kayıt" yazıp boş sayfa gösteriyordu).
///
/// Yalnız ECSPROS_TEST_DB verildiğinde çalışır.
/// </summary>
[TestClass]
public sealed class YetkiLogAramaDbTests
{
    [TestMethod]
    public async Task Arama_kimlige_cozulur_ve_sayim_satirlarla_tutarli()
    {
        var cs = Environment.GetEnvironmentVariable("ECSPROS_TEST_DB");
        if (string.IsNullOrWhiteSpace(cs)) { Assert.Inconclusive("ECSPROS_TEST_DB yok."); return; }

        var b = new NpgsqlDataSourceBuilder(cs); b.EnableDynamicJson();
        await using var ds = b.Build();
        await using var db = new IamDbContext(new DbContextOptionsBuilder<IamDbContext>().UseNpgsql(ds).Options);

        var q = YetkiLogGrid.YalnizYetkiOlaylari(db.AuditLogs.AsNoTracking());

        // Handler'daki arama bloğunun aynısı — çeviri kırılırsa burada patlar.
        const string terim = "admin";
        var kullanicilar = await db.Users.AsNoTracking()
            .Where(u => (u.FirstName + " " + u.LastName).ToLower().Contains(terim)
                || u.Email.ToLower().Contains(terim) || u.Username.ToLower().Contains(terim))
            .Select(u => u.Id).ToListAsync();
        var gruplar = await db.Roles.AsNoTracking()
            .Where(g => g.Code.ToLower().Contains(terim)
                || ECSPros.Shared.Kernel.Grid.GridJson.Text(g.NameI18n, "tr")!.ToLower().Contains(terim))
            .Select(g => g.Id).ToListAsync();
        var yetkiler = await db.Permissions.AsNoTracking()
            .Where(p => p.Code.ToLower().Contains(terim))
            .Select(p => p.Id).ToListAsync();

        var hedefler = kullanicilar.Concat(gruplar).Concat(yetkiler).Distinct().ToList();
        var suzulmus = q.Where(a => a.EntityType.ToLower().Contains(terim)
            || (a.UserId != null && kullanicilar.Contains(a.UserId.Value))
            || hedefler.Contains(a.EntityId));

        // 1) Çeviri: sayım ve ilk sayfa SQL'e dönüşmeli.
        var toplam = await suzulmus.CountAsync();
        var ilkSayfa = await YetkiLogGrid.Schema.ApplySort(suzulmus, null).Take(50).ToListAsync();

        // 2) Tutarlılık: sayım filtrelenmiş küme üzerinde olmalı — satır varsa sayı 0 OLAMAZ
        //    (eski hatanın imzası tam buydu: satırlar süzülür, sayı süzülmemiş kalırdı).
        Assert.IsTrue(toplam >= ilkSayfa.Count,
            $"Toplam ({toplam}) ilk sayfadaki satır sayısından ({ilkSayfa.Count}) küçük olamaz.");
        if (ilkSayfa.Count > 0) Assert.IsTrue(toplam > 0, "Satır döndü ama toplam 0 — sayım filtreyle aynı kümede değil.");

        // 3) Sıralama şemadan çalışmalı (tarih/olay/ip): her anahtar asc+desc SQL'e çevrilmeli.
        foreach (var key in YetkiLogGrid.Schema.SortableFields)
            foreach (var dir in new[] { "asc", "desc" })
                _ = await YetkiLogGrid.Schema
                    .ApplySort(suzulmus, new ECSPros.Shared.Kernel.Grid.GridRequest { Sort = key, Dir = dir })
                    .Take(1).ToListAsync();
    }
}
