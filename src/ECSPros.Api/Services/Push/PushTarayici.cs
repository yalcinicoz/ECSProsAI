using ECSPros.Shared.Contracts;
using ECSPros.Storefront.Application.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ECSPros.Api.Services.Push;

/// <summary>
/// Zamanlanmış push senaryoları (§4.2-§4.6 + §4.1 payment_pending/review_invite), 15 dk'da bir (worker). Her tarama
/// kendi try/catch'inde; tekilleştirme PushKuyruk'ta (dedupId). Ham SQL ile şemalar arası okuma (Api katmanı).
/// favorite_back_in_stock önceki stok durumu bilgisi gerektirdiğinden ilk sürümde YOK (favorite_low_stock ve stock_alert var).
/// </summary>
public sealed class PushTarayici(NpgsqlDataSource ds, IStorefrontDbContext sdb, PushKuyruk kuyruk, IStockService stok, IEffectivePriceProvider fiyat,
    IProductService urun, IConfiguration config, ILogger<PushTarayici> logger)
{
    static readonly TimeZoneInfo TrTz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
    string Bugun => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TrTz).ToString("yyyy-MM-dd");
    string Hafta => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TrTz).ToString("yyyy") + "-w" + System.Globalization.ISOWeek.GetWeekOfYear(DateTime.UtcNow);
    static string Para(decimal v) => v.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("tr-TR")) + " ₺";

    public async Task TaraAsync(CancellationToken ct)
    {
        var adimlar = new (string Ad, Func<CancellationToken, Task<int>> F)[]
        {
            ("stock_alert", StokAlarmiAsync), ("favorite", FavoriAsync), ("cart_reminder", SepetHatirlatmaAsync),
            ("coupon", KuponAsync), ("wallet_credit", CuzdanAsync), ("welcome", HosGeldinAsync), ("winback", WinbackAsync),
            ("viewed_reminder", GezilenAsync), ("order_payment_pending", OdemeBekleyenAsync), ("order_review_invite", DegerlendirmeDavetiAsync),
        };
        foreach (var (ad, f) in adimlar)
        {
            try { var n = await f(ct); if (n > 0) logger.LogInformation("Push tarama {Ad}: {N} bildirim kuyruğa alındı", ad, n); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { logger.LogWarning(ex, "Push tarama {Ad} hata", ad); }
        }
    }

    // ── yardımcılar ──
    sealed record UrunBilgi(string Code, string Name, Guid? VariantId, string? ImageUrl);
    async Task<Dictionary<string, UrunBilgi>> UrunlerAsync(IEnumerable<string> codes, CancellationToken ct)
    {
        var list = codes.Distinct().ToList(); var m = new Dictionary<string, UrunBilgi>();
        if (list.Count == 0) return m;
        await using var c = await ds.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT p."Code", COALESCE(p."NameI18n"->>'tr',''), (SELECT v."Id" FROM catalog.product_variants v WHERE v."ProductId"=p."Id" AND NOT v."IsDeleted" ORDER BY v."CreatedAt" LIMIT 1)
              FROM catalog.products p WHERE p."Code" = ANY(@c) AND NOT p."IsDeleted"
            """, c);
        cmd.Parameters.AddWithValue("c", list.ToArray());
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) m[r.GetString(0)] = new(r.GetString(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetGuid(2), null);
        var vids = m.Values.Where(x => x.VariantId != null).Select(x => x.VariantId!.Value).ToList();
        if (vids.Count > 0)
        {
            var disp = await urun.GetVariantDisplayAsync(vids, ct);
            foreach (var k in m.Keys.ToList()) if (m[k].VariantId is { } v && disp.TryGetValue(v, out var d)) m[k] = m[k] with { ImageUrl = d.ImageUrl };
        }
        return m;
    }
    async Task<List<T>> SorguAsync<T>(string sql, Func<NpgsqlDataReader, T> map, CancellationToken ct, params (string, object)[] prm)
    {
        var list = new List<T>();
        await using var c = await ds.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, c) { CommandTimeout = 120 };
        foreach (var (k, v) in prm) cmd.Parameters.AddWithValue(k, v);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(map(r));
        return list;
    }
    static Dictionary<string, string> V(params (string, string?)[] kv) => kv.Where(x => x.Item2 != null).ToDictionary(x => x.Item1, x => x.Item2!);

    // ── §4.2 stock_alert (İ): "gelince haber ver" kaydı olan varyant stoğa girdi → gönder + alarmı kapat ──
    async Task<int> StokAlarmiAsync(CancellationToken ct)
    {
        var alarmlar = await sdb.StockAlerts.Where(a => a.Status == "active").ToListAsync(ct);
        if (alarmlar.Count == 0) return 0;
        var stoklar = await stok.GetVariantAvailableStocksAsync(ct);
        int n = 0;
        var gelenler = alarmlar.Where(a => stoklar.GetValueOrDefault(a.VariantId) > 0).ToList();
        if (gelenler.Count == 0) return 0;
        var disp = await urun.GetVariantDisplayAsync(gelenler.Select(a => a.VariantId).Distinct().ToList(), ct);
        foreach (var a in gelenler)
        {
            disp.TryGetValue(a.VariantId, out var d);
            var code = d?.ProductCode ?? a.ProductCode ?? "";
            n += await kuyruk.EnqueueAsync(new PushIstek("stock_alert", a.MemberId, V(("productName", d?.ProductNameI18n.GetValueOrDefault("tr") ?? ""), ("variantInfo", d?.OptionsText ?? a.VariantInfo ?? ""), ("productCode", code), ("alertId", a.Id.ToString())),
                $"stock_alert:{a.Id}", a.FirmPlatformId, ImageUrl: d?.ImageUrl), ct);
            a.Status = "notified"; a.NotifiedAt = DateTime.UtcNow;
        }
        await sdb.SaveChangesAsync(ct);
        return n;
    }

    // ── §4.2 favorite_price_drop (P, ≥%10 eşik, üye başına günde 1 = en büyük düşüş) + favorite_low_stock (P, ≤3, haftada 1) ──
    async Task<int> FavoriAsync(CancellationToken ct)
    {
        var favs = await sdb.Favorites.ToListAsync(ct);
        if (favs.Count == 0) return 0;
        var esik = config.GetValue("Push:PriceDropPercent", 10m) / 100m;
        var dusukEsik = config.GetValue("Push:LowStockThreshold", 3);
        var urunler = await UrunlerAsync(favs.Select(f => f.ProductCode), ct);
        // ürün kimliği + varyantları (stok toplamı için)
        var pv = await SorguAsync("""
            SELECT p."Code", p."Id", v."Id" FROM catalog.products p JOIN catalog.product_variants v ON v."ProductId"=p."Id" AND NOT v."IsDeleted" WHERE p."Code"=ANY(@c) AND NOT p."IsDeleted"
            """,
            r => (Code: r.GetString(0), PId: r.GetGuid(1), VId: r.GetGuid(2)), ct, ("c", favs.Select(f => f.ProductCode).Distinct().ToArray()));
        var pidByCode = pv.GroupBy(x => x.Code).ToDictionary(g => g.Key, g => g.First().PId);
        var varByCode = pv.GroupBy(x => x.Code).ToDictionary(g => g.Key, g => g.Select(x => x.VId).ToList());
        var stoklar = await stok.GetVariantAvailableStocksAsync(ct);
        var fiyatlar = new Dictionary<Guid, Dictionary<Guid, decimal>>();
        int n = 0; var degisti = false;
        foreach (var uyeGrup in favs.GroupBy(f => f.MemberId))
        {
            (Storefront.Domain.Entities.Favorite F, decimal Eski, decimal Yeni)? enIyi = null;
            foreach (var f in uyeGrup)
            {
                if (!pidByCode.TryGetValue(f.ProductCode, out var pid)) continue;
                if (!fiyatlar.TryGetValue(f.FirmPlatformId, out var pf)) fiyatlar[f.FirmPlatformId] = pf = await fiyat.GetMinEffectivePricesAsync(f.FirmPlatformId, ct);
                if (!pf.TryGetValue(pid, out var simdiki) || simdiki <= 0) continue;
                if (f.PriceAtAdd is null) { f.PriceAtAdd = simdiki; degisti = true; continue; }
                var dusus = f.PriceAtAdd.Value - simdiki;
                if (dusus > 0 && dusus >= f.PriceAtAdd.Value * esik && (enIyi is null || dusus > enIyi.Value.Eski - enIyi.Value.Yeni)) enIyi = (f, f.PriceAtAdd.Value, simdiki);
                // düşük stok
                if (varByCode.TryGetValue(f.ProductCode, out var vids))
                {
                    var toplam = vids.Sum(v => stoklar.GetValueOrDefault(v));
                    if (toplam > 0 && toplam <= dusukEsik)
                    {
                        var u = urunler.GetValueOrDefault(f.ProductCode);
                        n += await kuyruk.EnqueueAsync(new PushIstek("favorite_low_stock", f.MemberId, V(("productName", u?.Name ?? f.ProductCode), ("productCode", f.ProductCode)),
                            $"favorite_low_stock:{f.MemberId}:{f.ProductCode}:{Hafta}", f.FirmPlatformId, ImageUrl: u?.ImageUrl), ct);
                    }
                }
            }
            if (enIyi is { } e)
            {
                var u = urunler.GetValueOrDefault(e.F.ProductCode);
                var yeni = e.Yeni.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                n += await kuyruk.EnqueueAsync(new PushIstek("favorite_price_drop", e.F.MemberId,
                    V(("productName", u?.Name ?? e.F.ProductCode), ("productCode", e.F.ProductCode), ("newPrice", Para(e.Yeni)), ("oldPrice", Para(e.Eski))),
                    $"favorite_price_drop:{e.F.MemberId}:{e.F.ProductCode}:{yeni}", e.F.FirmPlatformId, ImageUrl: u?.ImageUrl), ct);
            }
        }
        if (degisti) await sdb.SaveChangesAsync(ct);
        return n;
    }

    // ── §4.3 cart_reminder (P): sepette ürün var, 3 saattir güncellenmedi, sipariş verilmedi; 24 saat sonra ikinci ve son ──
    async Task<int> SepetHatirlatmaAsync(CancellationToken ct)
    {
        var saat = config.GetValue("Push:CartReminderHours", 3);
        var sepetler = await SorquSepetAsync(saat, ct);
        int n = 0;
        foreach (var s in sepetler)
        {
            var d = (await urun.GetVariantDisplayAsync([s.VariantId], ct)).GetValueOrDefault(s.VariantId);
            var ikinci = s.UpdatedAt <= DateTime.UtcNow.AddHours(-24);
            var vars = V(("productName", d?.ProductNameI18n.GetValueOrDefault("tr") ?? "Ürün"), ("n", Math.Max(0, s.Adet - 1).ToString()), ("cartId", s.CartId.ToString()));
            n += await kuyruk.EnqueueAsync(new PushIstek("cart_reminder", s.MemberId, vars, $"cart_reminder:{s.CartId}:{s.UpdatedAt.Ticks}{(ikinci ? ":2" : "")}", s.FirmPlatformId, ImageUrl: d?.ImageUrl), ct);
        }
        return n;
    }
    Task<List<(Guid CartId, Guid MemberId, Guid FirmPlatformId, DateTime UpdatedAt, int Adet, Guid VariantId)>> SorquSepetAsync(int saat, CancellationToken ct)
        => SorguAsync("""
            SELECT c."Id", c."MemberId", c."FirmPlatformId", COALESCE(c."UpdatedAt", c."CreatedAt") u,
                   (SELECT COUNT(*)::int FROM crm.crm_cart_items i WHERE i."CartId"=c."Id" AND NOT i."IsDeleted"),
                   (SELECT i."VariantId" FROM crm.crm_cart_items i WHERE i."CartId"=c."Id" AND NOT i."IsDeleted" ORDER BY i."AddedAt" DESC LIMIT 1)
              FROM crm.crm_carts c
             WHERE NOT c."IsDeleted" AND c."MemberId" IS NOT NULL
               AND COALESCE(c."UpdatedAt", c."CreatedAt") <= now() - make_interval(hours => @h)
               AND COALESCE(c."UpdatedAt", c."CreatedAt") >= now() - interval '3 days'
               AND EXISTS (SELECT 1 FROM crm.crm_cart_items i WHERE i."CartId"=c."Id" AND NOT i."IsDeleted")
               AND NOT EXISTS (SELECT 1 FROM "order".ord_orders o WHERE o."MemberId"=c."MemberId" AND NOT o."IsDeleted" AND o."CreatedAt" >= COALESCE(c."UpdatedAt", c."CreatedAt"))
            """, r => (r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetDateTime(3), r.GetInt32(4), r.GetGuid(5)), ct, ("h", saat));

    // ── §4.5 coupon_assigned (üyeye özel kupon, son 24 saat) + coupon_expiring (kullanılmamış, 24 saat kaldı) ──
    async Task<int> KuponAsync(CancellationToken ct)
    {
        var rows = await SorguAsync("""
            SELECT c."Id", c."MemberId", c."Code", c."DiscountValue", c."CouponType", c."EndsAt", c."CreatedAt", c."UsageCount"
              FROM promotion.prm_coupons c
             WHERE NOT c."IsDeleted" AND c."IsActive" AND c."MemberId" IS NOT NULL
               AND (c."CreatedAt" >= now() - interval '24 hours' OR (c."UsageCount"=0 AND c."EndsAt" IS NOT NULL AND c."EndsAt" BETWEEN now() AND now() + interval '24 hours'))
            """, r => (Id: r.GetGuid(0), MemberId: r.GetGuid(1), Code: r.GetString(2), Deger: r.GetDecimal(3), Tip: r.GetString(4), Bitis: r.IsDBNull(5) ? (DateTime?)null : r.GetDateTime(5), Olusturma: r.GetDateTime(6), Kullanim: r.GetInt32(7)), ct);
        int n = 0;
        foreach (var c in rows)
        {
            var indirim = c.Tip.Contains("percent", StringComparison.OrdinalIgnoreCase) ? $"%{c.Deger:0.##} indirim" : $"{Para(c.Deger)} indirim";
            var vars = V(("couponCode", c.Code), ("discountText", indirim), ("expiresAt", c.Bitis?.ToString("dd.MM.yyyy")));
            if (c.Olusturma >= DateTime.UtcNow.AddHours(-24))
                n += await kuyruk.EnqueueAsync(new PushIstek("coupon_assigned", c.MemberId, vars, $"coupon_assigned:{c.Id}"), ct);
            if (c.Kullanim == 0 && c.Bitis is { } b && b <= DateTime.UtcNow.AddHours(24) && b > DateTime.UtcNow)
                n += await kuyruk.EnqueueAsync(new PushIstek("coupon_expiring", c.MemberId, vars, $"coupon_expiring:{c.Id}"), ct);
        }
        return n;
    }

    // ── §4.5 wallet_credit: son 24 saatte cüzdana yükleme ──
    async Task<int> CuzdanAsync(CancellationToken ct)
    {
        var rows = await SorguAsync("""
            SELECT t."Id", w."MemberId", t."Credit" FROM crm.crm_wallet_transactions t JOIN crm.crm_wallets w ON w."Id"=t."WalletId"
             WHERE NOT t."IsDeleted" AND t."Credit" > 0 AND t."CreatedAt" >= now() - interval '24 hours'
            """, r => (Id: r.GetGuid(0), MemberId: r.GetGuid(1), Tutar: r.GetDecimal(2)), ct);
        int n = 0;
        foreach (var t in rows) n += await kuyruk.EnqueueAsync(new PushIstek("wallet_credit", t.MemberId, V(("amount", Para(t.Tutar))), $"wallet_credit:{t.Id}"), ct);
        return n;
    }

    // ── §4.6 welcome: kayıttan 1-25 saat sonra, siparişi yoksa ──
    async Task<int> HosGeldinAsync(CancellationToken ct)
    {
        var rows = await SorguAsync("""
            SELECT m."Id" FROM crm.crm_members m WHERE NOT m."IsDeleted" AND m."CreatedAt" BETWEEN now() - interval '25 hours' AND now() - interval '1 hour'
               AND NOT EXISTS (SELECT 1 FROM "order".ord_orders o WHERE o."MemberId"=m."Id" AND NOT o."IsDeleted")
               AND EXISTS (SELECT 1 FROM storefront.push_devices d WHERE d."MemberId"=m."Id" AND d."Status"='active' AND NOT d."IsDeleted")
            """, r => r.GetGuid(0), ct);
        int n = 0;
        foreach (var m in rows) n += await kuyruk.EnqueueAsync(new PushIstek("welcome", m, V(), $"welcome:{m}"), ct);
        return n;
    }

    // ── §4.6 winback: 30 gündür açılış yok (push_devices.LastSeenAt) — ayda 1 ──
    async Task<int> WinbackAsync(CancellationToken ct)
    {
        var rows = await SorguAsync("""
            SELECT DISTINCT d."MemberId" FROM storefront.push_devices d WHERE d."MemberId" IS NOT NULL AND d."Status"='active' AND NOT d."IsDeleted"
             GROUP BY d."MemberId" HAVING MAX(d."LastSeenAt") < now() - interval '30 days'
            """, r => r.GetGuid(0), ct);
        var ay = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TrTz).ToString("yyyy-MM");
        int n = 0;
        foreach (var m in rows) n += await kuyruk.EnqueueAsync(new PushIstek("winback", m, V(), $"winback:{m}:{ay}"), ct);
        return n;
    }

    // ── §4.6 viewed_reminder: son 24 saatte gezdi, sepete eklemedi — günde 1 (haftalık tip sınırı 2) ──
    async Task<int> GezilenAsync(CancellationToken ct)
    {
        var rows = await SorguAsync("""
            SELECT v."MemberId", v."FirmPlatformId", v."ProductCode" FROM storefront.viewed_products v
             WHERE NOT v."IsDeleted" AND v."ViewedAt" BETWEEN now() - interval '24 hours' AND now() - interval '2 hours'
               AND NOT EXISTS (SELECT 1 FROM crm.crm_carts c JOIN crm.crm_cart_items i ON i."CartId"=c."Id" WHERE c."MemberId"=v."MemberId" AND i."AddedAt" >= now() - interval '24 hours')
             ORDER BY v."ViewedAt" DESC
            """, r => (MemberId: r.GetGuid(0), Platform: r.GetGuid(1), Code: r.GetString(2)), ct);
        int n = 0;
        foreach (var g in rows.GroupBy(x => x.MemberId))
        {
            var ilk = g.First();
            var u = (await UrunlerAsync([ilk.Code], ct)).GetValueOrDefault(ilk.Code);
            n += await kuyruk.EnqueueAsync(new PushIstek("viewed_reminder", g.Key, V(("productName", u?.Name ?? ""), ("productCode", ilk.Code)), $"viewed_reminder:{g.Key}:{Bugun}", ilk.Platform, ImageUrl: u?.ImageUrl), ct);
        }
        return n;
    }

    // ── §4.1 order_payment_pending: kart siparişi 60 dk-24 saat önce oluştu, ödeme tamamlanmadı (bir kez) ──
    async Task<int> OdemeBekleyenAsync(CancellationToken ct)
    {
        var rows = await SorguAsync("""
            SELECT o."Id", o."MemberId", o."OrderNumber", o."FirmPlatformId" FROM "order".ord_orders o
             WHERE NOT o."IsDeleted" AND o."MemberId" IS NOT NULL AND o."PaymentMethod"='kart' AND o."PaymentStatus" IN ('pending','unpaid')
               AND o."Status" NOT IN ('cancelled') AND o."CreatedAt" BETWEEN now() - interval '24 hours' AND now() - interval '60 minutes'
            """, r => (Id: r.GetGuid(0), MemberId: r.GetGuid(1), No: r.GetString(2), Platform: r.GetGuid(3)), ct);
        int n = 0;
        foreach (var o in rows) n += await kuyruk.EnqueueAsync(new PushIstek("order_payment_pending", o.MemberId, V(("orderNumber", o.No), ("orderId", o.Id.ToString())), $"order_payment_pending:{o.Id}", o.Platform), ct);
        return n;
    }

    // ── §4.1 order_review_invite: teslim + 2 gün (5 güne kadar), üye siparişteki ürünleri değerlendirmediyse ──
    async Task<int> DegerlendirmeDavetiAsync(CancellationToken ct)
    {
        var rows = await SorguAsync("""
            SELECT o."Id", o."MemberId", o."FirmPlatformId",
                   (SELECT i."VariantId" FROM "order".ord_order_items i WHERE i."OrderId"=o."Id" ORDER BY i."UnitPrice" DESC LIMIT 1)
              FROM "order".ord_orders o
             WHERE NOT o."IsDeleted" AND o."MemberId" IS NOT NULL AND o."Status"='delivered'
               AND COALESCE(o."UpdatedAt", o."CreatedAt") BETWEEN now() - interval '5 days' AND now() - interval '2 days'
               AND NOT EXISTS (SELECT 1 FROM storefront.product_reviews r WHERE r."MemberId"=o."MemberId" AND r."CreatedAt" >= o."CreatedAt")
            """, r => (Id: r.GetGuid(0), MemberId: r.GetGuid(1), Platform: r.GetGuid(2), VariantId: r.IsDBNull(3) ? (Guid?)null : r.GetGuid(3)), ct);
        int n = 0;
        foreach (var o in rows)
        {
            var d = o.VariantId is { } v ? (await urun.GetVariantDisplayAsync([v], ct)).GetValueOrDefault(v) : null;
            n += await kuyruk.EnqueueAsync(new PushIstek("order_review_invite", o.MemberId, V(("productName", d?.ProductNameI18n.GetValueOrDefault("tr") ?? "Ürünlerin"), ("orderId", o.Id.ToString())), $"order_review_invite:{o.Id}", o.Platform, ImageUrl: d?.ImageUrl), ct);
        }
        return n;
    }
}
