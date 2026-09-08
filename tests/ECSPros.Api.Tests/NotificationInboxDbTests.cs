using ECSPros.Api.Services.Push;
using ECSPros.Crm.Infrastructure.Persistence;
using ECSPros.Storefront.Domain.Entities;
using ECSPros.Storefront.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace ECSPros.Api.Tests;

/// <summary>
/// Uygulama içi "Bildirimlerim" (docs/BILDIRIMLERIM_BACKEND_ISTEGI.md §7 test prosedürü) — gerçek DB'de, TEK işlem içinde ve
/// sonunda GERİ ALINARAK (hiçbir satır kalmaz). Yalnız ECSPROS_TEST_DB verildiğinde çalışır. Üye kimliği rastgele GUID'dir
/// (gerçek üye gerekmez; kuyruk, cihazı olmayan üyeye cihazsız 'skipped' satır açar).
/// </summary>
[TestClass]
public sealed class NotificationInboxDbTests
{
    static NpgsqlDataSource? Ds()
    {
        var cs = Environment.GetEnvironmentVariable("ECSPROS_TEST_DB");
        if (string.IsNullOrWhiteSpace(cs)) return null;
        var b = new NpgsqlDataSourceBuilder(cs); b.EnableDynamicJson(); return b.Build();
    }
    static DbContextOptions<T> Opt<T>(NpgsqlDataSource ds) where T : DbContext => new DbContextOptionsBuilder<T>().UseNpgsql(ds).Options;

    [TestMethod]
    public async Task Bildirimlerim_liste_okundu_sil_akisi_ve_yetki_sinirlari()
    {
        var ds = Ds();
        if (ds is null) { Assert.Inconclusive("ECSPROS_TEST_DB verilmedi — DB testi atlandı."); return; }
        await using var sdb = new StorefrontDbContext(Opt<StorefrontDbContext>(ds));
        await using var cdb = new CrmDbContext(Opt<CrmDbContext>(ds));
        await using var tx = await sdb.Database.BeginTransactionAsync();   // cdb yalnız okur (izin), ayrı bağlantı
        try
        {
            var uye = Guid.NewGuid(); var baskasi = Guid.NewGuid(); var platform = Guid.NewGuid();
            var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Push:Enabled"] = "true" }).Build();
            var kuyruk = new PushKuyruk(sdb, cdb, cfg, NullLogger<PushKuyruk>.Instance);
            var kutu = new BildirimKutusu(sdb);

            // şablon ayarı: campaign türü seed'de yok → geçici şablon (dismissOnOpen) — işlem içinde, geri alınır
            var kampanya = await sdb.PushTemplates.FirstOrDefaultAsync(x => x.Type == "campaign");
            if (kampanya is null) sdb.PushTemplates.Add(new PushTemplate { Type = "campaign", Class = "marketing", Name = "Kampanya", Title = "Kampanya", Body = "Fırsat!", LinkTemplate = "/", Icon = "campaign", ExpiresDays = 30, DismissOnOpen = true });
            else { kampanya.Enabled = true; kampanya.Inbox = true; kampanya.DismissOnOpen = true; kampanya.Icon = "campaign"; }
            await sdb.SaveChangesAsync();

            // §7.1: cihazı olmayan üyeye 3 bildirim → FCM'e gidemez (skipped) ama listede
            var V = (params (string k, string v)[] p) => p.ToDictionary(x => x.k, x => x.v);
            Assert.AreEqual(0, await kuyruk.EnqueueAsync(new PushIstek("order_shipped", uye, V(("orderNumber", "MIS0000061"), ("cargoName", "MNG"), ("trackingNumber", "123"), ("orderId", Guid.NewGuid().ToString())), $"t:order_shipped:{uye}", platform, ImageUrl: "https://cdn/x.webp"), default));
            Assert.AreEqual(0, await kuyruk.EnqueueAsync(new PushIstek("favorite_price_drop", uye, V(("productName", "Elbise"), ("productCode", "P-1"), ("newPrice", "10"), ("oldPrice", "20")), $"t:favorite:{uye}", platform, ImageUrl: "https://cdn/y.webp"), default));
            Assert.AreEqual(0, await kuyruk.EnqueueAsync(new PushIstek("campaign", uye, V(), $"t:campaign:{uye}", platform), default));
            // aynı dedupId ikinci kez → yeni satır yok
            await kuyruk.EnqueueAsync(new PushIstek("campaign", uye, V(), $"t:campaign:{uye}", platform), default);
            // başka üyenin satırı
            await kuyruk.EnqueueAsync(new PushIstek("order_shipped", baskasi, V(("orderNumber", "X"), ("orderId", Guid.NewGuid().ToString())), $"t:other:{baskasi}", platform), default);
            // süresi dolmuş satır (§7.8) — doğrudan
            sdb.PushNotifications.Add(new PushNotification { MemberId = uye, FirmPlatformId = platform, Platform = "inbox", Type = "welcome", Class = "marketing", DedupId = $"t:expired:{uye}", Title = "Eski", Body = "Eski", Link = "/", Status = "skipped", ScheduledAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(-1), Icon = "campaign" });
            await sdb.SaveChangesAsync();
            sdb.ChangeTracker.Clear();

            // §7.2 liste: 3 satır, unread 3, yeni→eski, ikon/görsel/sınıf
            var s1 = await kutu.ListeAsync(uye, platform, 1, 20, default);
            Assert.AreEqual(3, s1.TotalCount); Assert.AreEqual(3, s1.UnreadCount); Assert.AreEqual(3, s1.Items.Count); Assert.IsFalse(s1.HasNextPage);
            Assert.AreEqual("campaign", s1.Items[0].Type, "sıra yeni→eski"); Assert.IsTrue(s1.Items[0].DismissOnOpen);
            var shipped = s1.Items.Single(x => x.Type == "order_shipped"); var fav = s1.Items.Single(x => x.Type == "favorite_price_drop"); var camp = s1.Items[0];
            Assert.AreEqual("cargo", shipped.Icon); Assert.AreEqual("transactional", shipped.Class); Assert.AreEqual("https://cdn/x.webp", shipped.ImageUrl);
            Assert.AreEqual("favorite", fav.Icon); Assert.AreEqual("marketing", fav.Class);
            Assert.IsTrue(shipped.Body.Contains("MIS0000061")); Assert.AreEqual($"t:order_shipped:{uye}", shipped.DedupId);
            // sayfalama
            var s1b = await kutu.ListeAsync(uye, platform, 1, 2, default); Assert.AreEqual(2, s1b.Items.Count); Assert.IsTrue(s1b.HasNextPage); Assert.AreEqual(2, s1b.TotalPages);
            Assert.AreEqual(BildirimKutusu.MaxPageSize, (await kutu.ListeAsync(uye, platform, 1, 500, default)).PageSize, "pageSize tavanı 50");

            // §7.3 okundu (idempotent)
            Assert.IsTrue(await kutu.OkunduAsync(uye, shipped.Id, default)); Assert.AreEqual(2, await kutu.OkunmamisAsync(uye, platform, default));
            Assert.IsTrue(await kutu.OkunduAsync(uye, shipped.Id, default)); Assert.AreEqual(2, await kutu.OkunmamisAsync(uye, platform, default));
            // §7.4 dismissOnOpen: okununca listeden düşer
            Assert.IsTrue(await kutu.OkunduAsync(uye, camp.Id, default));
            var s2 = await kutu.ListeAsync(uye, platform, 1, 20, default);
            Assert.AreEqual(1, s2.UnreadCount); Assert.AreEqual(2, s2.TotalCount); Assert.IsFalse(s2.Items.Any(x => x.Id == camp.Id));
            // §7.5 sil
            Assert.IsTrue(await kutu.SilAsync(uye, fav.Id, default));
            var s3 = await kutu.ListeAsync(uye, platform, 1, 20, default);
            Assert.AreEqual(0, s3.UnreadCount); Assert.AreEqual(1, s3.Items.Count); Assert.AreEqual(shipped.Id, s3.Items[0].Id); Assert.IsNotNull(s3.Items[0].ReadAt);
            Assert.IsTrue(await kutu.SilAsync(uye, fav.Id, default), "zaten silinmiş → idempotent 200");
            // §7.7 yabancı id → 404 (false); başka üyenin listesi etkilenmedi
            var yabanci = (await kutu.ListeAsync(baskasi, platform, 1, 5, default)).Items[0].Id;
            Assert.IsFalse(await kutu.OkunduAsync(uye, yabanci, default), "başka üyenin satırı → 404");
            Assert.IsFalse(await kutu.SilAsync(uye, yabanci, default), "başka üyenin satırı → 404");
            Assert.IsFalse(await kutu.SilAsync(uye, Guid.NewGuid(), default));
            Assert.AreEqual(1, (await kutu.ListeAsync(baskasi, platform, 1, 5, default)).UnreadCount);
            // §7.6 tümü okundu / tümü sil
            await kuyruk.EnqueueAsync(new PushIstek("order_confirmed", uye, V(("orderNumber", "Y"), ("orderId", Guid.NewGuid().ToString())), $"t:confirmed:{uye}", platform), default);
            sdb.ChangeTracker.Clear();
            Assert.AreEqual(1, await kutu.OkunmamisAsync(uye, platform, default));
            await kutu.TumunuOkunduAsync(uye, platform, default); Assert.AreEqual(0, await kutu.OkunmamisAsync(uye, platform, default));
            await kutu.TumunuSilAsync(uye, platform, default);
            var s4 = await kutu.ListeAsync(uye, platform, 1, 20, default); Assert.AreEqual(0, s4.TotalCount); Assert.AreEqual(0, s4.UnreadCount);
            // §7.8 süresi dolan hiç görünmedi (toplamlar yukarıda 3'tü)
            // §1 inbox=false şablon → liste satırı açılmaz
            var t = await sdb.PushTemplates.FirstAsync(x => x.Type == "order_confirmed"); t.Inbox = false; await sdb.SaveChangesAsync(); sdb.ChangeTracker.Clear();
            await kuyruk.EnqueueAsync(new PushIstek("order_confirmed", uye, V(("orderNumber", "Z"), ("orderId", Guid.NewGuid().ToString())), $"t:confirmed2:{uye}", platform), default);
            Assert.AreEqual(0, (await kutu.ListeAsync(uye, platform, 1, 20, default)).TotalCount, "inbox=false şablon listeye girmez");
        }
        finally { await tx.RollbackAsync(); }
    }
}
