using System.Globalization;
using System.Text;
using System.Text.Json;
using MySql.Data.MySqlClient;
using Npgsql;

namespace ECSPros.Api.Services.Legacy;

/// <summary>
/// F1 (2026-08-04): eski sisteme sipariş yazma dilimi — plan:
/// docs/eski-sistem-siparis-senkron-plani.md. Outbox (integration.legacy_order_outbox)
/// 'create' işlerini işler: siparişten ECSGYE.ClassLibrary.Order form modeli kurulur ve
/// eski sitenin BasicAuth korumalı POST /Services/SiparisOlusturFromModel ucuna gönderilir
/// (action FORM binding kullanır — JSON body değil; model indeksli form anahtarlarıyla gider).
/// Eski uç sipariş Id dönmez → yazım sonrası MySQL'den (platformId, orderNumber) ile okunur
/// ve ord_orders.LegacyOrderId'ye işlenir. Uç idempotenttir (kayıtlıysa mevcut Id döner).
///
/// KADEMELER: Legacy:Sync:Enabled + kanal legacyPlatformId ayarı → dilim koşar; varsayılan
/// Legacy:Sync:DryRun=true → form modeli YALNIZ integration_logs'a yazılır (uca gitmez),
/// outbox satırı dry_run'da bekler. Gerçek gönderim için ek olarak DryRun=false +
/// Legacy:OrderService:User/Password dolu olmalı.
///
/// Bilinen sınırlar (F1): taksit sayısı 0 yazılır; kapıda bedeli expense satırı için
/// Legacy:OrderService:CodExpenseTypeId ayarı gerekir (0 = expense satırı yazılmaz, tutar
/// yine expenseTotal/orderTotal'dadır); ondalık ayracı varsayılan NOKTA —
/// eski sunucu kültürü virgülle bind ediyorsa Legacy:OrderService:DecimalComma=true yapılır
/// (dry-run doğrulamasında netleşir).
/// </summary>
public sealed class LegacyOrderSyncService(
    NpgsqlDataSource dataSource,
    IConfiguration config,
    IHttpClientFactory httpClientFactory,
    ILogger<LegacyOrderSyncService> logger)
{
    private string MySqlConn => config["Legacy:MySqlConnection"] ?? "";
    // DİKKAT: B dilimlerinin Legacy:Sync:DryRun bayrağından BAĞIMSIZ — canlıda katalog
    // senkronu gerçek modda çalışırken sipariş yazımı ayrıca ve bilinçli açılır (F4).
    private bool DryRun => config.GetValue("Legacy:Sync:OrderDryRun", true);
    private string ServiceUrl => (config["Legacy:OrderService:Url"] ?? "https://services.misharitalia.com").TrimEnd('/');
    private string ServiceUser => config["Legacy:OrderService:User"] ?? "";
    private string ServicePass => config["Legacy:OrderService:Password"] ?? "";
    private string Kaynak => config["Legacy:OrderService:Kaynak"] ?? "website";
    private int CodExpenseTypeId => config.GetValue("Legacy:OrderService:CodExpenseTypeId", 2);
    private bool DecimalComma => config.GetValue("Legacy:OrderService:DecimalComma", false);
    private const int MaxAttempt = 5;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(MySqlConn);

    public async Task<LegacySyncService.Report> SyncOrderOutboxAsync(CancellationToken ct)
    {
        var t0 = DateTime.UtcNow;
        var log = new StringBuilder();
        var islenen = 0;
        try
        {
            await using var pg = await dataSource.OpenConnectionAsync(ct);

            // İşlenecekler: pending her zaman; dry_run yalnız gerçek mod açıldıysa; error tekrar denenir
            var isler = new List<(Guid id, Guid orderId, string jobType, int attempt)>();
            await using (var cmd = new NpgsqlCommand($"""
                SELECT "Id", "OrderId", "JobType", "AttemptCount"
                FROM integration.legacy_order_outbox
                WHERE "IsDeleted" = false
                  AND ("Status" = 'pending'
                       OR ("Status" = 'error' AND "AttemptCount" < {MaxAttempt})
                       OR ("Status" = 'dry_run' AND {(DryRun ? "false" : "true")}))
                ORDER BY "CreatedAt"
                LIMIT 20
                """, pg))
            await using (var r = await cmd.ExecuteReaderAsync(ct))
                while (await r.ReadAsync(ct))
                    isler.Add((r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetInt32(3)));

            if (isler.Count == 0)
                return new(true, DryRun, "orders", 0, "Kuyruk boş.", null, Ms(t0));

            foreach (var job in isler)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var sonuc = job.jobType == "cancel"
                        ? await IptalIsleAsync(pg, job.orderId, log, ct)
                        : await IsleAsync(pg, job.orderId, log, ct);
                    await OutboxGuncelleAsync(pg, job.id, sonuc.durum, job.attempt + 1, sonuc.hata, ct);
                    if (sonuc.durum is "done" or "dry_run") islenen++;
                }
                catch (Exception ex)
                {
                    log.AppendLine($"! {job.orderId}: {ex.Message}");
                    await OutboxGuncelleAsync(pg, job.id, "error", job.attempt + 1, Kes(ex.Message, 1900), ct);
                }
            }

            return new(true, DryRun, "orders", islenen, log.ToString(), null, Ms(t0));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Legacy sipariş senkron dilimi hatası");
            return new(false, DryRun, "orders", islenen, log.ToString(), ex.Message, Ms(t0));
        }
    }

    private async Task<(string durum, string? hata)> IsleAsync(
        NpgsqlConnection pg, Guid orderId, StringBuilder log, CancellationToken ct)
    {
        var veri = await SiparisOkuAsync(pg, orderId, ct);
        if (veri is null) return ("error", "Sipariş bulunamadı.");
        if (veri.LegacyOrderId is not null)
        {
            log.AppendLine($"= {veri.OrderNumber}: zaten eskide (Id={veri.LegacyOrderId}).");
            return ("done", null);
        }
        if (veri.Status == "cancelled")
        {
            log.AppendLine($"= {veri.OrderNumber}: yazılmadan iptal edildi — eskiye gönderilmiyor.");
            return ("done", null);
        }
        if (veri.LegacyPlatformId is null)
            return ("error", "Kanalın legacyPlatformId ayarı yok.");

        // Legacy varyant eşlemesi (barkod=SKU üzerinden) + KDV tip Id'leri MySQL'den
        await using var my = new MySqlConnection(MySqlConn);
        await my.OpenAsync(ct);
        var barkodlar = veri.Items.Select(i => i.Sku).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
        var varyantMap = await LegacyVaryantlarAsync(my, barkodlar, ct);
        var eksik = veri.Items.Where(i => !varyantMap.ContainsKey(i.Sku)).Select(i => i.Sku).Distinct().ToList();
        if (eksik.Count > 0)
            return ("error", $"Eski sistemde varyant bulunamadı (barkod): {string.Join(", ", eksik.Take(5))}");
        var kdvTipMap = await KdvTipleriAsync(my, ct);

        var form = FormKur(veri, varyantMap, kdvTipMap);

        // integration_logs kaydı — dry-run'da model doğrulama buradan yapılır
        var payloadJson = JsonSerializer.Serialize(
            form.Select(kv => new { k = kv.Key, v = kv.Value }),
            new JsonSerializerOptions { WriteIndented = false });

        if (DryRun)
        {
            await LogYazAsync(pg, orderId, "dry_run", payloadJson, null, ct);
            log.AppendLine($"~ {veri.OrderNumber}: DRY-RUN form modeli üretildi ({form.Count} alan).");
            return ("dry_run", null);
        }

        if (string.IsNullOrWhiteSpace(ServiceUser))
            return ("error", "Legacy:OrderService:User/Password yapılandırılmadı.");

        // Gerçek gönderim
        var client = httpClientFactory.CreateClient("legacy-order");
        using var istek = new HttpRequestMessage(HttpMethod.Post, $"{ServiceUrl}/Services/SiparisOlusturFromModel")
        { Content = new FormUrlEncodedContent(form) };
        istek.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ServiceUser}:{ServicePass}")));
        using var cevap = await client.SendAsync(istek, ct);
        var cevapMetni = await cevap.Content.ReadAsStringAsync(ct);
        if (!cevap.IsSuccessStatusCode)
        {
            await LogYazAsync(pg, orderId, "failure", payloadJson, $"HTTP {(int)cevap.StatusCode}: {Kes(cevapMetni, 500)}", ct);
            return ("error", $"Eski servis HTTP {(int)cevap.StatusCode} döndü.");
        }

        // Uç Id dönmez — MySQL'den doğrula ve al (yazım gerçekleşmediyse hata)
        var legacyId = await LegacySiparisIdAsync(my, veri.LegacyPlatformId.Value, veri.OrderNumber, ct);
        if (legacyId == 0)
        {
            await LogYazAsync(pg, orderId, "failure", payloadJson, "Servis 200 döndü ama oporders'ta kayıt bulunamadı (form binding?).", ct);
            return ("error", "Servis 200 döndü ama eski DB'de sipariş oluşmadı.");
        }

        await using (var upd = new NpgsqlCommand(
            """UPDATE "order".ord_orders SET "LegacyOrderId" = @lid, "UpdatedAt" = now() WHERE "Id" = @id""", pg))
        {
            upd.Parameters.AddWithValue("lid", legacyId);
            upd.Parameters.AddWithValue("id", orderId);
            await upd.ExecuteNonQueryAsync(ct);
        }
        await LogYazAsync(pg, orderId, "success", payloadJson, null, ct);
        log.AppendLine($"+ {veri.OrderNumber} → eski sipariş Id={legacyId}");
        return ("done", null);
    }

    // ─── F3: müşteri iptalini eskiye taşı ────────────────────────────────
    private async Task<(string durum, string? hata)> IptalIsleAsync(
        NpgsqlConnection pg, Guid orderId, StringBuilder log, CancellationToken ct)
    {
        var veri = await SiparisOkuAsync(pg, orderId, ct);
        if (veri is null) return ("error", "Sipariş bulunamadı.");
        if (veri.LegacyOrderId is null)
        {
            log.AppendLine($"= {veri.OrderNumber}: eskiye hiç yazılmamış — iptal işi kapatıldı.");
            return ("done", null);
        }
        if (veri.LegacyPlatformId is null)
            return ("error", "Kanalın legacyPlatformId ayarı yok.");

        if (DryRun)
        {
            await LogYazAsync(pg, orderId, "dry_run",
                $"{{\"job\":\"cancel\",\"orderNumber\":\"{veri.OrderNumber}\",\"legacyOrderId\":{veri.LegacyOrderId}}}", null, ct);
            log.AppendLine($"~ {veri.OrderNumber}: DRY-RUN iptal planlandı (legacyId={veri.LegacyOrderId}).");
            return ("dry_run", null);
        }
        if (string.IsNullOrWhiteSpace(ServiceUser))
            return ("error", "Legacy:OrderService:User/Password yapılandırılmadı.");

        await using var my = new MySqlConnection(MySqlConn);
        await my.OpenAsync(ct);

        int legacyMemberId; string eskiDurum;
        await using (var cmd = new MySqlCommand(
            "SELECT memberId, orderStatus FROM oporders WHERE Id=@id LIMIT 1", my))
        {
            cmd.Parameters.AddWithValue("@id", veri.LegacyOrderId.Value);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return ("error", "Eski sipariş kaydı bulunamadı.");
            legacyMemberId = r.GetInt32(0);
            eskiDurum = r.IsDBNull(1) ? "" : r.GetString(1);
        }
        if (eskiDurum == "İptal Edildi")
        {
            log.AppendLine($"= {veri.OrderNumber}: eskide zaten iptal.");
            return ("done", null);
        }

        var client = httpClientFactory.CreateClient("legacy-order");
        using var istek = new HttpRequestMessage(HttpMethod.Post, $"{ServiceUrl}/Services/UyeSiparisIptal")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["orderNumber"] = veri.OrderNumber,
                ["memberId"] = legacyMemberId.ToString(),
                ["platformId"] = veri.LegacyPlatformId.Value.ToString()
            })
        };
        istek.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ServiceUser}:{ServicePass}")));
        using var cevap = await client.SendAsync(istek, ct);
        var cevapMetni = await cevap.Content.ReadAsStringAsync(ct);
        if (!cevap.IsSuccessStatusCode)
        {
            await LogYazAsync(pg, orderId, "failure", "cancel", $"HTTP {(int)cevap.StatusCode}: {Kes(cevapMetni, 500)}", ct);
            return ("error", $"Eski iptal servisi HTTP {(int)cevap.StatusCode} döndü.");
        }

        // Doğrulama: eski durum gerçekten İptal Edildi mi? ("Hazırlanıyor" sonrası eski kural
        // iptali reddedebilir — o zaman iş error'da kalır, operasyon eski panelde çözer.)
        await using (var kontrol = new MySqlCommand(
            "SELECT orderStatus FROM oporders WHERE Id=@id LIMIT 1", my))
        {
            kontrol.Parameters.AddWithValue("@id", veri.LegacyOrderId.Value);
            var v = await kontrol.ExecuteScalarAsync(ct);
            if (v?.ToString() != "İptal Edildi")
            {
                await LogYazAsync(pg, orderId, "failure", "cancel", Kes($"Eski taraf iptali uygulamadı: {cevapMetni}", 900), ct);
                return ("error", $"Eski taraf iptali uygulamadı (durum: {v}).");
            }
        }

        await LogYazAsync(pg, orderId, "success", $"cancel legacyId={veri.LegacyOrderId}", null, ct);
        log.AppendLine($"+ {veri.OrderNumber}: eskide iptal edildi (Id={veri.LegacyOrderId}).");
        return ("done", null);
    }

    // ─── F2: Durum + kargo geri senkronu (eski → yeni) ───────────────────
    // Eski paneldeki operasyon ilerledikçe LegacyOrderId'li açık siparişlerin durumu
    // buraya taşınır. STOK YAN ETKİSİZ: durum raw UPDATE ile yazılır (domain event yok —
    // stok otoritesi eski sistemde; kargo/iptal/iade geçişinde rezervasyon 'released'
    // yapılır ve Stock.ReservedQuantity düşülür ama Quantity'ye ASLA dokunulmaz).
    // Yalnız İLERİ yön uygulanır; eski panelde geri alma görülürse log'a düşer.
    private static readonly Dictionary<string, (string yeni, int sira)> DurumEslemesi = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Onay Bekliyor"] = ("pending", 0),
        ["Beklemede"] = ("pending", 0),
        ["Hazırlanıyor"] = ("processing", 2),
        ["Faturası Kesildi"] = ("processing", 2),
        ["Kargoya Verildi"] = ("shipped", 3),
        ["Teslim Edildi"] = ("delivered", 4),
        ["İptal Edildi"] = ("cancelled", 9),
    };
    private static readonly Dictionary<string, int> YeniDurumSirasi = new()
    { ["pending"] = 0, ["confirmed"] = 1, ["processing"] = 2, ["shipped"] = 3, ["delivered"] = 4 };

    public async Task<LegacySyncService.Report> SyncOrderStatusAsync(CancellationToken ct)
    {
        var t0 = DateTime.UtcNow;
        var log = new StringBuilder();
        var degisen = 0;
        try
        {
            await using var pg = await dataSource.OpenConnectionAsync(ct);

            var acikler = new List<(Guid id, int legacyId, string durum, string no)>();
            await using (var cmd = new NpgsqlCommand("""
                SELECT "Id", "LegacyOrderId", "Status", "OrderNumber"
                FROM "order".ord_orders
                WHERE "LegacyOrderId" IS NOT NULL
                  AND "Status" NOT IN ('delivered','cancelled','returned')
                LIMIT 500
                """, pg))
            await using (var r = await cmd.ExecuteReaderAsync(ct))
                while (await r.ReadAsync(ct))
                    acikler.Add((r.GetGuid(0), r.GetInt32(1), r.GetString(2), r.GetString(3)));

            if (acikler.Count == 0)
                return new(true, false, "order-status", 0, "Açık eski-bağlı sipariş yok.", null, Ms(t0));

            // Eski durumlar tek sorguda
            var eski = new Dictionary<int, (string durum, string? kargoAd, string? takipNo, string? faturaNo, DateTime? teslim)>();
            await using (var my = new MySqlConnection(MySqlConn))
            {
                await my.OpenAsync(ct);
                var idler = string.Join(",", acikler.Select(a => a.legacyId));
                await using var cmd = new MySqlCommand($"""
                    SELECT Id, orderStatus, courierName,
                           COALESCE(NULLIF(courierTrackingNumber,''), shippingBarcode) takipNo,
                           invoiceNumber, deliveryDate
                    FROM oporders WHERE Id IN ({idler})
                    """, my);
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                    eski[r.GetInt32(0)] = (
                        r.IsDBNull(1) ? "" : r.GetString(1),
                        r.IsDBNull(2) ? null : r.GetString(2),
                        r.IsDBNull(3) ? null : r.GetString(3),
                        r.IsDBNull(4) ? null : r.GetString(4),
                        r.IsDBNull(5) ? null : r.GetDateTime(5));
            }

            foreach (var o in acikler)
            {
                ct.ThrowIfCancellationRequested();
                if (!eski.TryGetValue(o.legacyId, out var e)) continue;

                (string yeni, int sira) hedef;
                if (e.durum.Contains("İade", StringComparison.OrdinalIgnoreCase))
                    hedef = ("returned", 9); // "Teslim Edilmeden/Edilemeden İade (Geldi)" varyantları
                else if (!DurumEslemesi.TryGetValue(e.durum.Trim(), out hedef))
                {
                    log.AppendLine($"? {o.no}: bilinmeyen eski durum '{e.durum}' — atlandı.");
                    continue;
                }

                if (hedef.yeni == o.durum) continue;
                var mevcutSira = YeniDurumSirasi.GetValueOrDefault(o.durum, 0);
                if (hedef.sira != 9 && hedef.sira <= mevcutSira)
                {
                    log.AppendLine($"< {o.no}: geri yönlü geçiş ({o.durum} → {hedef.yeni}) uygulanmadı.");
                    continue;
                }

                await DurumUygulaAsync(pg, o.id, o.no, hedef.yeni, e, ct);
                log.AppendLine($"+ {o.no}: {o.durum} → {hedef.yeni}" +
                    (e.takipNo is { Length: > 0 } ? $" (takip: {e.takipNo})" : ""));
                degisen++;
            }

            return new(true, false, "order-status", degisen, log.ToString(), null, Ms(t0));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Legacy durum senkron dilimi hatası");
            return new(false, false, "order-status", degisen, log.ToString(), ex.Message, Ms(t0));
        }
    }

    private static async Task DurumUygulaAsync(
        NpgsqlConnection pg, Guid orderId, string orderNumber, string yeniDurum,
        (string durum, string? kargoAd, string? takipNo, string? faturaNo, DateTime? teslim) e, CancellationToken ct)
    {
        await using var tx = await pg.BeginTransactionAsync(ct);

        // Durum + iz alanları (ham eski durum ve fatura no CustomerNotes jsonb'sine —
        // "Faturası Kesildi" müşteriye "Hazırlanıyor" görünür, iç iz burada durur)
        await using (var cmd = new NpgsqlCommand("""
            UPDATE "order".ord_orders SET
                "Status" = @s,
                "ConfirmedAt" = CASE WHEN @s IN ('processing','shipped','delivered') THEN COALESCE("ConfirmedAt", now()) ELSE "ConfirmedAt" END,
                "CustomerNotes" = COALESCE("CustomerNotes", '{}'::jsonb)
                    || jsonb_build_object('legacyStatus', @raw::text)
                    || CASE WHEN @inv::text IS NULL THEN '{}'::jsonb ELSE jsonb_build_object('legacyInvoiceNumber', @inv::text) END,
                "UpdatedAt" = now()
            WHERE "Id" = @id
            """, pg, tx))
        {
            cmd.Parameters.AddWithValue("s", yeniDurum);
            cmd.Parameters.AddWithValue("raw", e.durum);
            cmd.Parameters.AddWithValue("inv", (object?)e.faturaNo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("id", orderId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Kargoya verildi/teslim: müşteri takip modalı ord_shipments'tan okur — eski kargo
        // bilgisiyle tek shipment satırı (yoksa) + durum olayı eklenir.
        if (yeniDurum is "shipped" or "delivered")
        {
            Guid shipmentId;
            await using (var bul = new NpgsqlCommand(
                """SELECT "Id" FROM "order".ord_shipments WHERE "OrderId" = @oid LIMIT 1""", pg, tx))
            {
                bul.Parameters.AddWithValue("oid", orderId);
                var v = await bul.ExecuteScalarAsync(ct);
                shipmentId = v is Guid g ? g : Guid.Empty;
            }
            if (shipmentId == Guid.Empty)
            {
                shipmentId = Guid.NewGuid();
                await using var ins = new NpgsqlCommand("""
                    INSERT INTO "order".ord_shipments
                        ("Id","OrderId","FirmIntegrationId","ShipmentNumber","TrackingNumber","Status",
                         "ApiStatus","DeliveredAt","DeliveryNotes","PackageCount","CreatedAt","IsDeleted")
                    VALUES (@id,@oid,'00000000-0000-0000-0000-000000000000',@no,@takip,@st,
                            'legacy',@teslim,@not,1,now(),false)
                    """, pg, tx);
                ins.Parameters.AddWithValue("id", shipmentId);
                ins.Parameters.AddWithValue("oid", orderId);
                ins.Parameters.AddWithValue("no", $"LEG-{orderNumber}");
                ins.Parameters.AddWithValue("takip", (object?)e.takipNo ?? DBNull.Value);
                ins.Parameters.AddWithValue("st", yeniDurum);
                ins.Parameters.AddWithValue("teslim", yeniDurum == "delivered" ? (object)(e.teslim ?? DateTime.UtcNow) : DBNull.Value);
                ins.Parameters.AddWithValue("not", (object?)e.kargoAd ?? DBNull.Value);
                await ins.ExecuteNonQueryAsync(ct);
            }
            else
            {
                await using var upd = new NpgsqlCommand("""
                    UPDATE "order".ord_shipments SET
                        "TrackingNumber" = COALESCE(@takip, "TrackingNumber"),
                        "Status" = @st,
                        "DeliveredAt" = CASE WHEN @st = 'delivered' THEN COALESCE("DeliveredAt", @teslim) ELSE "DeliveredAt" END,
                        "UpdatedAt" = now()
                    WHERE "Id" = @id
                    """, pg, tx);
                upd.Parameters.AddWithValue("takip", (object?)e.takipNo ?? DBNull.Value);
                upd.Parameters.AddWithValue("st", yeniDurum);
                upd.Parameters.AddWithValue("teslim", e.teslim ?? DateTime.UtcNow);
                upd.Parameters.AddWithValue("id", shipmentId);
                await upd.ExecuteNonQueryAsync(ct);
            }

            // Zaman çizelgesine olay satırı (modal Events listesi)
            await using (var ev = new NpgsqlCommand("""
                INSERT INTO "order".ord_shipment_events
                    ("Id","ShipmentId","EventCode","EventDescription","EventDate","CreatedAt")
                VALUES (gen_random_uuid(), @sid, @kod, @aciklama, now(), now())
                """, pg, tx))
            {
                ev.Parameters.AddWithValue("sid", shipmentId);
                ev.Parameters.AddWithValue("kod", yeniDurum == "delivered" ? "delivered" : "shipped");
                ev.Parameters.AddWithValue("aciklama", yeniDurum == "delivered"
                    ? "Paket teslim edildi (eski sistem)."
                    : $"Paket kargoya verildi{(e.kargoAd is { Length: > 0 } ? $" — {e.kargoAd}" : "")}.");
                await ev.ExecuteNonQueryAsync(ct);
            }
        }

        // Kargo/iptal/iade: bekleyen rezervasyonlar bırakılır (ReservedQuantity düşer,
        // Quantity'ye dokunulmaz — gerçek stok B2 dilimiyle eski sistemden gelir)
        if (yeniDurum is "shipped" or "cancelled" or "returned")
        {
            await using (var stok = new NpgsqlCommand("""
                UPDATE inventory.inv_stocks s
                SET "ReservedQuantity" = GREATEST(0, s."ReservedQuantity" - r."Quantity"), "UpdatedAt" = now()
                FROM inventory.inv_stock_reservations r
                WHERE r."StockId" = s."Id" AND r."ReferenceType" = 'order'
                  AND r."ReferenceId" = @oid AND r."Status" = 'reserved'
                """, pg, tx))
            {
                stok.Parameters.AddWithValue("oid", orderId);
                await stok.ExecuteNonQueryAsync(ct);
            }
            await using (var rez = new NpgsqlCommand("""
                UPDATE inventory.inv_stock_reservations
                SET "Status" = 'released', "UpdatedAt" = now()
                WHERE "ReferenceType" = 'order' AND "ReferenceId" = @oid AND "Status" = 'reserved'
                """, pg, tx))
            {
                rez.Parameters.AddWithValue("oid", orderId);
                await rez.ExecuteNonQueryAsync(ct);
            }
        }

        await tx.CommitAsync(ct);
    }

    // ─── Sipariş verisi (PG) ─────────────────────────────────────────────
    private sealed record Kalem(Guid Id, string Sku, string ProductName, string VariantInfo,
        int Quantity, decimal UnitPrice, decimal DiscountAmount, decimal TaxRate);

    private sealed record SiparisVerisi(
        string OrderNumber, string Status, Guid FirmPlatformId, int? LegacyPlatformId, int? LegacyOrderId,
        string? PaymentMethod, string Currency, decimal Subtotal, decimal TotalDiscount,
        decimal TotalExpense, decimal GrandTotal, DateTime CreatedAt,
        string RecipientName, string RecipientPhone, string AddressLine, string? PostalCode,
        string CityName, string DistrictName, string? NeighborhoodName,
        string? MemberFirstName, string? MemberLastName, string? MemberEmail, string? MemberPhone,
        string? MemberIdentityNumber, int? LegacyMemberId, Guid? MemberId, string? CustomerNote,
        List<Kalem> Items);

    private async Task<SiparisVerisi?> SiparisOkuAsync(NpgsqlConnection pg, Guid orderId, CancellationToken ct)
    {
        const string sql = """
            SELECT o."OrderNumber", o."Status", o."FirmPlatformId", o."LegacyOrderId", o."PaymentMethod",
                   o."CurrencyCode", o."Subtotal", o."TotalDiscount", o."TotalExpense", o."GrandTotal",
                   o."CreatedAt", o."ShippingRecipientName", o."ShippingRecipientPhone",
                   o."ShippingAddressLine", o."ShippingPostalCode",
                   COALESCE(c."NameI18n"->>'tr',''), COALESCE(d."NameI18n"->>'tr',''), n."NameI18n"->>'tr',
                   m."FirstName", m."LastName", m."Email", m."Phone", m."IdentityNumber", m."LegacyMemberId",
                   o."MemberId", o."CustomerNotes"->>'note', fp."Settings"
            FROM "order".ord_orders o
            LEFT JOIN crm.crm_cities c ON c."Id" = o."ShippingCityId"
            LEFT JOIN crm.crm_districts d ON d."Id" = o."ShippingDistrictId"
            LEFT JOIN crm.crm_neighborhoods n ON n."Id" = o."ShippingNeighborhoodId"
            LEFT JOIN crm.crm_members m ON m."Id" = o."MemberId"
            LEFT JOIN core.core_firm_platforms fp ON fp."Id" = o."FirmPlatformId"
            WHERE o."Id" = @id
            """;
        await using var cmd = new NpgsqlCommand(sql, pg);
        cmd.Parameters.AddWithValue("id", orderId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;

        int? legacyPlatform = null;
        if (!r.IsDBNull(26))
        {
            var settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(r.GetString(26));
            if (settings is not null && settings.TryGetValue("legacyPlatformId", out var lp)
                && lp.ValueKind == JsonValueKind.Number)
                legacyPlatform = lp.GetInt32();
        }

        var veri = new SiparisVerisi(
            r.GetString(0), r.GetString(1), r.GetGuid(2), legacyPlatform,
            r.IsDBNull(3) ? null : r.GetInt32(3),
            r.IsDBNull(4) ? null : r.GetString(4),
            r.GetString(5), r.GetDecimal(6), r.GetDecimal(7), r.GetDecimal(8), r.GetDecimal(9),
            r.GetDateTime(10), r.GetString(11), r.GetString(12), r.GetString(13),
            r.IsDBNull(14) ? null : r.GetString(14),
            r.GetString(15), r.GetString(16),
            r.IsDBNull(17) ? null : r.GetString(17),
            r.IsDBNull(18) ? null : r.GetString(18),
            r.IsDBNull(19) ? null : r.GetString(19),
            r.IsDBNull(20) ? null : r.GetString(20),
            r.IsDBNull(21) ? null : r.GetString(21),
            r.IsDBNull(22) ? null : r.GetString(22),
            r.IsDBNull(23) ? null : r.GetInt32(23),
            r.IsDBNull(24) ? null : r.GetGuid(24),
            r.IsDBNull(25) ? null : r.GetString(25),
            new List<Kalem>());
        await r.CloseAsync();

        // DİKKAT (dry-run doğrulaması 2026-08-04): ord_order_items.Sku ÜRÜN KODU taşıyor
        // (checkout istemcisi öyle gönderiyor) — legacy eşleşme anahtarı VARYANTIN barkodu
        // (B1 kuralı: yeni varyantlarda Sku=barkod; Barcode kolonu öncelikli, Sku yedek).
        const string itemSql = """
            SELECT i."Id",
                   COALESCE(NULLIF(v."Barcode",''), NULLIF(v."Sku",''), i."Sku") AS barkod,
                   i."ProductName", i."VariantInfo", i."Quantity",
                   i."UnitPrice", i."DiscountAmount", COALESCE(p."TaxRate", 10)
            FROM "order".ord_order_items i
            LEFT JOIN catalog.product_variants v ON v."Id" = i."VariantId"
            LEFT JOIN catalog.products p ON p."Id" = v."ProductId"
            WHERE i."OrderId" = @id
            ORDER BY i."CreatedAt"
            """;
        await using var icmd = new NpgsqlCommand(itemSql, pg);
        icmd.Parameters.AddWithValue("id", orderId);
        await using var ir = await icmd.ExecuteReaderAsync(ct);
        while (await ir.ReadAsync(ct))
            veri.Items.Add(new Kalem(ir.GetGuid(0), ir.GetString(1), ir.GetString(2),
                ir.IsDBNull(3) ? "" : ir.GetString(3), ir.GetInt32(4),
                ir.GetDecimal(5), ir.GetDecimal(6), ir.GetInt32(7)));
        return veri;
    }

    // ─── MySQL yardımcıları ──────────────────────────────────────────────
    private sealed record LegacyVaryant(int Id, string UrunKodu, string? Varyant1, string? Varyant2);

    private static async Task<Dictionary<string, LegacyVaryant>> LegacyVaryantlarAsync(
        MySqlConnection my, List<string> barkodlar, CancellationToken ct)
    {
        var map = new Dictionary<string, LegacyVaryant>();
        if (barkodlar.Count == 0) return map;
        var liste = string.Join("','", barkodlar.Select(b => b.Replace("'", "")));
        await using var cmd = new MySqlCommand($"""
            SELECT v.barkod, v.Id, u.urunKodu, v.varyant1Degeri, v.varyant2Degeri
            FROM apurunvaryantlari v INNER JOIN apurunler u ON v.urunId = u.Id
            WHERE v.barkod IN ('{liste}')
            """, my);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            map[r.GetString(0)] = new LegacyVaryant(r.GetInt32(1), r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4));
        return map;
    }

    private static async Task<Dictionary<double, int>> KdvTipleriAsync(MySqlConnection my, CancellationToken ct)
    {
        var map = new Dictionary<double, int>();
        await using var cmd = new MySqlCommand("SELECT Id, rate FROM dftaxtypes", my);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var oran = r.GetDouble(1);
            if (!map.ContainsKey(oran)) map[oran] = r.GetInt32(0);
        }
        return map;
    }

    private static async Task<int> LegacySiparisIdAsync(MySqlConnection my, int platformId, string orderNumber, CancellationToken ct)
    {
        await using var cmd = new MySqlCommand(
            "SELECT Id FROM oporders WHERE platformId=@p AND orderNumber=@no LIMIT 1", my);
        cmd.Parameters.AddWithValue("@p", platformId);
        cmd.Parameters.AddWithValue("@no", orderNumber);
        var v = await cmd.ExecuteScalarAsync(ct);
        return v is null or DBNull ? 0 : Convert.ToInt32(v);
    }

    // ─── ECSGYE.ClassLibrary.Order form modeli ───────────────────────────
    private List<KeyValuePair<string, string>> FormKur(
        SiparisVerisi v, Dictionary<string, LegacyVaryant> varyantMap, Dictionary<double, int> kdvTipMap)
    {
        var f = new List<KeyValuePair<string, string>>();
        void Ekle(string k, string? deger) { if (deger is not null) f.Add(new(k, deger)); }
        string Para(decimal d) => d.ToString("0.##", DecimalComma ? new CultureInfo("tr-TR") : CultureInfo.InvariantCulture);
        string Tarih(DateTime d) => d.ToString("yyyy-MM-ddTHH:mm:ss");

        var odemeTipi = v.PaymentMethod switch
        {
            "kapida-nakit" => 2,
            "kapida-kart" => 3,
            _ => 1 // kart/online (PayTR vb.) — kullanıcı kararı: hepsi kredi kartı
        };
        var kapida = odemeTipi is 2 or 3;
        var (adiIlk, adiSon) = AdSoyadAyir(v.RecipientName);

        Ekle("orderNumber", v.OrderNumber);
        Ekle("platformId", v.LegacyPlatformId!.Value.ToString());
        Ekle("sourcePlatformOrderNumber", v.OrderNumber);
        Ekle("orderStatus", "Onay Bekliyor"); // eski kural: her yeni sipariş böyle başlar
        Ekle("kaynak", Kaynak);
        Ekle("paymentTypeId", odemeTipi.ToString());
        Ekle("orderDate", Tarih(v.CreatedAt));
        Ekle("orderTime", v.CreatedAt.ToString("HH:mm"));
        Ekle("currency", "TL");
        Ekle("exchangeRate", "1");
        Ekle("invoiceType", "arsiv");
        Ekle("kargoGonder", "false");
        Ekle("topluFaturaKes", "false");
        Ekle("useGiftPackage", "false");
        Ekle("estimatedDueDate", Tarih(DateTime.Now.AddDays(3)));
        Ekle("createdDate", Tarih(DateTime.Now));
        Ekle("createdIP", "");
        Ekle("createdPersonnel.Id", "1");
        Ekle("customerNote", v.CustomerNote ?? "");
        // Eski SiparisAnaKaydiOlustur bu alanları NULL korumasız DataRow'a yazar —
        // bağlanmayan string null kalır ve 'Cannot set Column ... to be null' fırlatır
        // (F4 gerçek gönderiminde courierId ile yakalandı). Hepsi açıkça gönderilir.
        Ekle("packageNumber", "");
        Ekle("kaynakPlatformDurum", "");
        Ekle("giftpackageMessage", "");
        Ekle("invoiceNumber", "");
        Ekle("courierName", "");
        Ekle("courierId", "0");
        Ekle("shippingBarcode", "");
        Ekle("courierTrackingNumber", "");
        Ekle("updatedIP", "");
        Ekle("sepetHediye", "");
        Ekle("countryId", "1");
        Ekle("member.phoneCode", "90");

        // Üye: LegacyMemberId varsa doğrudan; yoksa eski taraf sourcePlatformMemberId ile
        // tekilleştirir/oluşturur (misafirde sipariş no bazlı — her misafir siparişi tek kayıt)
        Ekle("member.memberId", (v.LegacyMemberId ?? 0).ToString());
        Ekle("member.platformId", v.LegacyPlatformId.Value.ToString());
        Ekle("member.memberTypeId", "0");
        Ekle("member.firstName", Kes(v.MemberFirstName ?? adiIlk, 30));
        Ekle("member.lastName", Kes(v.MemberLastName ?? adiSon, 30));
        Ekle("member.email", Kes(v.MemberEmail ?? "", 100));
        Ekle("member.phone", Kes(v.MemberPhone ?? v.RecipientPhone, 20));
        Ekle("member.tcKimlikNo", Kes(v.MemberIdentityNumber ?? "", 11));
        Ekle("member.sourcePlatformMemberId",
            v.MemberId is { } mid ? $"ECS-{mid:N}" : $"ECS-GUEST-{v.OrderNumber}");
        // Eski CreateMemberFromModel üyenin il/ilçesini member.address'ten okur (Trendyol
        // kalıbı invoiceAddress'i bağlar) — bağlanmazsa NRE (F4 ilk gönderimde yakalandı).
        Ekle("member.address.cityName", Kes(v.CityName, 20));
        Ekle("member.address.districtName", Kes(string.IsNullOrWhiteSpace(v.DistrictName) ? v.CityName : v.DistrictName, 20));

        // Adresler (teslimat = fatura; ayrı fatura adresi F1 kapsamı dışı — arşiv fatura)
        foreach (var on in new[] { "shippingAddress", "invoiceAddress" })
        {
            Ekle($"{on}.contactFirstName", Kes(adiIlk, 30));
            Ekle($"{on}.contactLastName", Kes(adiSon, 30));
            Ekle($"{on}.contactEMail", Kes(v.MemberEmail ?? "", 100));
            Ekle($"{on}.contactPhone", Kes(v.RecipientPhone, 20));
            Ekle($"{on}.addressDetail", Kes(v.AddressLine + (v.NeighborhoodName is { } mh ? $" {mh}" : ""), 255));
            Ekle($"{on}.cityName", Kes(v.CityName, 20));
            Ekle($"{on}.districtName", Kes(string.IsNullOrWhiteSpace(v.DistrictName) ? v.CityName : v.DistrictName, 20));
            Ekle($"{on}.postalCode", Kes(v.PostalCode ?? "", 10));
            Ekle($"{on}.countryId", "1");
            Ekle($"{on}.countryName", "Türkiye");
        }

        // Kalemler: adet başına 1 satır (Trendyol kalıbı); indirim payı birimlere dağıtılır
        int satir = 0;
        double kdvToplam = 0;
        int vergiSatir = 0;
        foreach (var it in v.Items)
        {
            var lv = varyantMap[it.Sku];
            var birimIndirimler = BirimlereDagit(it.DiscountAmount, it.Quantity);
            for (var i = 0; i < it.Quantity; i++)
            {
                var anahtar = $"orderProducts[{satir}]";
                var sepetSatirId = $"{it.Id:N}-{i}";
                Ekle($"{anahtar}.basketDetailId", sepetSatirId);
                Ekle($"{anahtar}.productVariantId", lv.Id.ToString());
                Ekle($"{anahtar}.barcode", it.Sku);
                Ekle($"{anahtar}.sellingPrice", Para(it.UnitPrice));
                Ekle($"{anahtar}.quantity", "1");
                Ekle($"{anahtar}.collectedQuantity", "0");
                Ekle($"{anahtar}.discountAmount", Para(birimIndirimler[i]));
                Ekle($"{anahtar}.productCode", lv.UrunKodu);
                Ekle($"{anahtar}.productName", Kes(it.ProductName, 100));
                Ekle($"{anahtar}.color", Kes(lv.Varyant1 ?? "", 30));
                Ekle($"{anahtar}.variantValue", Kes(lv.Varyant2 ?? "", 30));
                Ekle($"{anahtar}.createdPersonnelId", "1");
                satir++;

                // Birim KDV (fiyata dahil) — ödenen birim fiyat üzerinden
                var birimNet = (double)(it.UnitPrice - birimIndirimler[i]);
                var oran = (double)it.TaxRate;
                var kdv = Math.Round(birimNet * oran / (100 + oran), 4);
                kdvToplam += kdv;
                var vk = $"orderTaxes[{vergiSatir}]";
                Ekle($"{vk}.basketDetailId", sepetSatirId);
                Ekle($"{vk}.gibCode", "0015");
                Ekle($"{vk}.gibType", "KDV");
                Ekle($"{vk}.taxRate", Para((decimal)oran));
                Ekle($"{vk}.taxAmount", Para((decimal)kdv));
                Ekle($"{vk}.taxBasis", Para((decimal)Math.Round(birimNet * 100 / (100 + oran), 4)));
                Ekle($"{vk}.currency", "TRY");
                Ekle($"{vk}.taxDescription", $"%{oran} KDV");
                Ekle($"{vk}.taxTypeId", (kdvTipMap.TryGetValue(oran, out var tid) ? tid : 0).ToString());
                vergiSatir++;
            }
        }

        // Kapıda bedeli masraf satırı (expenseTypeId ayarı verilmişse)
        if (kapida && v.TotalExpense > 0 && CodExpenseTypeId > 0)
        {
            Ekle("orderExpenses[0].expenseTypeId", CodExpenseTypeId.ToString());
            Ekle("orderExpenses[0].expenseAmount", Para(v.TotalExpense));
            Ekle("orderExpenses[0].expenseDescription", "Kapıda Ödeme Hizmet Bedeli");
        }

        // Toplamlar (bizim sunucu-hesaplı sipariş toplamlarımız)
        Ekle("productTotal", Para(v.Subtotal));
        Ekle("subTotal", Para(v.Subtotal));
        Ekle("discountTotal", Para(v.TotalDiscount));
        Ekle("expenseTotal", Para(v.TotalExpense));
        Ekle("taxTotal", Para((decimal)Math.Round(kdvToplam, 2)));
        Ekle("orderTotal", Para(v.GrandTotal));
        Ekle("paidTotal", kapida ? "0" : Para(v.GrandTotal));
        Ekle("payableAmount", kapida ? Para(v.GrandTotal) : "0");

        // Ödeme kaydı
        Ekle("orderPayments[0].paymentTypeId", odemeTipi.ToString());
        Ekle("orderPayments[0].isPaid", kapida ? "false" : "true");
        Ekle("orderPayments[0].paymentAmount", Para(v.GrandTotal));
        Ekle("orderPayments[0].installmentCount", "0");
        Ekle("orderPayments[0].paymentDescription", kapida
            ? (odemeTipi == 2 ? "Kapıda Ödeme Nakit" : "Kapıda Ödeme Kart")
            : "Kredi Kartı (Yeni Site)");
        Ekle("orderPayments[0].orderNumber", v.OrderNumber);

        return f;
    }

    // ─── Outbox / log yardımcıları ───────────────────────────────────────
    private static async Task OutboxGuncelleAsync(
        NpgsqlConnection pg, Guid id, string durum, int attempt, string? hata, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            UPDATE integration.legacy_order_outbox
            SET "Status" = @s, "AttemptCount" = @a, "LastError" = @e,
                "ProcessedAt" = CASE WHEN @s IN ('done','dry_run') THEN now() ELSE "ProcessedAt" END,
                "UpdatedAt" = now()
            WHERE "Id" = @id
            """, pg);
        cmd.Parameters.AddWithValue("s", durum);
        cmd.Parameters.AddWithValue("a", attempt);
        cmd.Parameters.AddWithValue("e", (object?)hata ?? DBNull.Value);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task LogYazAsync(
        NpgsqlConnection pg, Guid orderId, string durum, string payload, string? hata, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO integration.integration_logs
                ("Id", "FirmIntegrationId", "ServiceType", "OperationType", "Status", "DurationMs",
                 "RequestPayload", "ErrorMessage", "ReferenceId", "ReferenceType", "CreatedAt", "IsDeleted")
            VALUES (gen_random_uuid(), '00000000-0000-0000-0000-000000000000', 'legacy', 'order_sync',
                    @s, 0, @p, @e, @rid, 'Order', now(), false)
            """, pg);
        cmd.Parameters.AddWithValue("s", durum);
        cmd.Parameters.AddWithValue("p", payload.Length > 16000 ? payload[..16000] : payload);
        cmd.Parameters.AddWithValue("e", (object?)hata ?? DBNull.Value);
        cmd.Parameters.AddWithValue("rid", orderId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static List<decimal> BirimlereDagit(decimal toplam, int adet)
    {
        var sonuc = new List<decimal>();
        if (adet <= 0) return sonuc;
        var pay = Math.Round(toplam / adet, 2);
        for (var i = 0; i < adet - 1; i++) sonuc.Add(pay);
        sonuc.Add(Math.Round(toplam - pay * (adet - 1), 2)); // kuruş artığı son birime
        return sonuc;
    }

    private static (string, string) AdSoyadAyir(string tam)
    {
        tam = (tam ?? "").Trim();
        var i = tam.LastIndexOf(' ');
        return i < 0 ? (tam, "") : (tam[..i], tam[(i + 1)..]);
    }

    private static string Kes(string? s, int max) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max]);

    private static int Ms(DateTime t0) => (int)(DateTime.UtcNow - t0).TotalMilliseconds;
}
