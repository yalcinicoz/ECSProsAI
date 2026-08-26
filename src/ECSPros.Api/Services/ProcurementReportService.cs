using Npgsql;

namespace ECSPros.Api.Services;

/// <summary>
/// T6 Tedarik Raporu — dönemsel/istatistiksel mutabakat (İ4: KESİN DEĞİLDİR) + KPI'lar.
/// Cross-schema raw-SQL okuma katmanı (MarketplaceAdminService kalıbı, cache yok — yönetim ekranı).
/// Kaynaklar: SA (procurement.purchase_orders, OrderDate dönemde, iptaller hariç), Sayım
/// (sorting_entries.CreatedAt dönemde; tedarikçi partiden gelir — partisiz kayıtlar ayrı kova),
/// Fatura (partiye bağlı fin_supplier_invoices, parti ReceivedAt dönemde, fatura başına bir kez).
/// </summary>
public sealed class ProcurementReportService(NpgsqlDataSource dataSource)
{
    public sealed record SupplierLine(
        Guid? SupplierId, string SupplierTitle,
        int PoCount, decimal PoQuantity, decimal PoAmount,
        decimal CountedQuantity, decimal CountedCost,
        decimal InvoiceAmount,
        decimal DiffQuantity);           // sayım − SA (pozitif = fazla gönderim)

    public sealed record Kpis(
        double? AvgReceiptToCountHours,      // teslim → sayım (partili)
        double? AvgCountToOnSaleHours,       // sayım → satışa giriş
        int PendingCount, decimal PendingQuantity,
        int Pending0_2, int Pending3_7, int Pending7Plus,     // bekleyen yerleştirme yaş kovaları (gün)
        int PlacedNotOnSaleCount, decimal PlacedNotOnSaleQuantity,
        int OpenMissingCards, double? OldestMissingCardDays);

    public sealed record NotOnSaleRow(
        Guid EntryId, Guid VariantId, Guid ProductId, string ProductCode, string Name, string Sku,
        decimal Quantity, DateTime PlacedAt);

    public async Task<(List<SupplierLine> Lines, Kpis Kpis, List<NotOnSaleRow> NotOnSale)> GetAsync(
        DateTime from, DateTime to, Guid? supplierId, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var lines = new Dictionary<string, SupplierLine>();     // key: supplierId veya "-"
        static string K(Guid? id) => id?.ToString() ?? "-";
        void Upsert(Guid? sid, string title, Func<SupplierLine, SupplierLine> f)
        {
            var k = K(sid);
            lines.TryGetValue(k, out var cur);
            cur ??= new SupplierLine(sid, title, 0, 0, 0, 0, 0, 0, 0);
            lines[k] = f(cur);
        }

        // SA
        const string poSql = @"
            SELECT po.""SupplierId"", COALESCE(ca.""Title"",''), COUNT(DISTINCT po.""Id"")::int,
                   COALESCE(SUM(i.""Quantity""),0), COALESCE(SUM(i.""Quantity"" * i.""UnitPrice""),0)
            FROM procurement.purchase_orders po
            LEFT JOIN procurement.purchase_order_items i ON i.""PurchaseOrderId"" = po.""Id"" AND NOT i.""IsDeleted""
            LEFT JOIN accounts.current_accounts ca ON ca.""Id"" = po.""SupplierId""
            WHERE NOT po.""IsDeleted"" AND po.""Status"" <> 'cancelled'
              AND po.""OrderDate"" >= @from AND po.""OrderDate"" < @to
              AND (@sup IS NULL OR po.""SupplierId"" = @sup)
            GROUP BY po.""SupplierId"", ca.""Title""";
        await using (var cmd = Cmd(conn, poSql, from, to, supplierId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
            {
                var sid = r.GetGuid(0); var title = r.GetString(1); var c = r.GetInt32(2);
                var q = r.GetDecimal(3); var a = r.GetDecimal(4);
                Upsert(sid, title, cur => cur with { SupplierTitle = title, PoCount = cur.PoCount + c, PoQuantity = cur.PoQuantity + q, PoAmount = cur.PoAmount + a });
            }

        // Sayım (tedarikçi partiden; partisiz → null kova)
        const string cntSql = @"
            SELECT b.""SupplierId"", COALESCE(ca.""Title"",''),
                   COALESCE(SUM(e.""Quantity""),0),
                   COALESCE(SUM(e.""Quantity"" * COALESCE(e.""UnitCost"",0)),0)
            FROM procurement.sorting_entries e
            LEFT JOIN procurement.receipt_batches b ON b.""Id"" = e.""ReceiptBatchId""
            LEFT JOIN accounts.current_accounts ca ON ca.""Id"" = b.""SupplierId""
            WHERE NOT e.""IsDeleted"" AND e.""CreatedAt"" >= @from AND e.""CreatedAt"" < @to
              AND (@sup IS NULL OR b.""SupplierId"" = @sup)
            GROUP BY b.""SupplierId"", ca.""Title""";
        await using (var cmd = Cmd(conn, cntSql, from, to, supplierId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
            {
                Guid? sid = r.IsDBNull(0) ? null : r.GetGuid(0);
                var title = sid is null ? "— Partisiz —" : r.GetString(1);
                var q = r.GetDecimal(2); var cost = r.GetDecimal(3);
                Upsert(sid, title, cur => cur with { SupplierTitle = title, CountedQuantity = cur.CountedQuantity + q, CountedCost = cur.CountedCost + cost });
            }

        // Fatura (parti bağı; fatura başına bir kez)
        const string invSql = @"
            SELECT x.sid, COALESCE(ca.""Title"",''), COALESCE(SUM(x.""GrandTotal""),0)
            FROM (SELECT DISTINCT b.""SupplierId"" sid, f.""Id"", f.""GrandTotal""
                  FROM procurement.receipt_batches b
                  JOIN finance.fin_supplier_invoices f ON f.""Id"" = b.""SupplierInvoiceId""
                  WHERE NOT b.""IsDeleted"" AND b.""ReceivedAt"" >= @from AND b.""ReceivedAt"" < @to
                    AND (@sup IS NULL OR b.""SupplierId"" = @sup)) x
            LEFT JOIN accounts.current_accounts ca ON ca.""Id"" = x.sid
            GROUP BY x.sid, ca.""Title""";
        await using (var cmd = Cmd(conn, invSql, from, to, supplierId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
            {
                var sid = r.GetGuid(0); var title = r.GetString(1); var a = r.GetDecimal(2);
                Upsert(sid, title, cur => cur with { SupplierTitle = title, InvoiceAmount = cur.InvoiceAmount + a });
            }

        var result = lines.Values
            .Select(l => l with { DiffQuantity = l.CountedQuantity - l.PoQuantity })
            .OrderByDescending(l => l.CountedQuantity).ToList();

        // KPI'lar
        const string kpiSql = @"
            SELECT
              (SELECT AVG(EXTRACT(EPOCH FROM (e.""CreatedAt"" - b.""ReceivedAt""))/3600.0)
                 FROM procurement.sorting_entries e JOIN procurement.receipt_batches b ON b.""Id"" = e.""ReceiptBatchId""
                 WHERE NOT e.""IsDeleted"" AND e.""CreatedAt"" >= @from AND e.""CreatedAt"" < @to
                   AND (@sup IS NULL OR b.""SupplierId"" = @sup)),
              (SELECT AVG(EXTRACT(EPOCH FROM (e.""OnSaleAt"" - e.""CreatedAt""))/3600.0)
                 FROM procurement.sorting_entries e
                 LEFT JOIN procurement.receipt_batches b ON b.""Id"" = e.""ReceiptBatchId""
                 WHERE NOT e.""IsDeleted"" AND e.""OnSaleAt"" IS NOT NULL
                   AND e.""CreatedAt"" >= @from AND e.""CreatedAt"" < @to
                   AND (@sup IS NULL OR b.""SupplierId"" = @sup)),
              (SELECT COUNT(*)::int FROM procurement.sorting_entries e
                 LEFT JOIN procurement.receipt_batches b ON b.""Id"" = e.""ReceiptBatchId""
                 WHERE NOT e.""IsDeleted"" AND e.""PutawayStatus"" = 'pending' AND (@sup IS NULL OR b.""SupplierId"" = @sup)),
              (SELECT COALESCE(SUM(e.""Quantity""),0) FROM procurement.sorting_entries e
                 LEFT JOIN procurement.receipt_batches b ON b.""Id"" = e.""ReceiptBatchId""
                 WHERE NOT e.""IsDeleted"" AND e.""PutawayStatus"" = 'pending' AND (@sup IS NULL OR b.""SupplierId"" = @sup)),
              (SELECT COUNT(*)::int FROM procurement.sorting_entries e
                 LEFT JOIN procurement.receipt_batches b ON b.""Id"" = e.""ReceiptBatchId""
                 WHERE NOT e.""IsDeleted"" AND e.""PutawayStatus"" = 'pending' AND e.""CreatedAt"" > now() - interval '2 days'
                   AND (@sup IS NULL OR b.""SupplierId"" = @sup)),
              (SELECT COUNT(*)::int FROM procurement.sorting_entries e
                 LEFT JOIN procurement.receipt_batches b ON b.""Id"" = e.""ReceiptBatchId""
                 WHERE NOT e.""IsDeleted"" AND e.""PutawayStatus"" = 'pending'
                   AND e.""CreatedAt"" <= now() - interval '2 days' AND e.""CreatedAt"" > now() - interval '7 days'
                   AND (@sup IS NULL OR b.""SupplierId"" = @sup)),
              (SELECT COUNT(*)::int FROM procurement.sorting_entries e
                 LEFT JOIN procurement.receipt_batches b ON b.""Id"" = e.""ReceiptBatchId""
                 WHERE NOT e.""IsDeleted"" AND e.""PutawayStatus"" = 'pending' AND e.""CreatedAt"" <= now() - interval '7 days'
                   AND (@sup IS NULL OR b.""SupplierId"" = @sup)),
              (SELECT COUNT(*)::int FROM procurement.sorting_entries e
                 LEFT JOIN procurement.receipt_batches b ON b.""Id"" = e.""ReceiptBatchId""
                 WHERE NOT e.""IsDeleted"" AND e.""PutawayStatus"" = 'placed' AND e.""OnSaleAt"" IS NULL
                   AND (@sup IS NULL OR b.""SupplierId"" = @sup)),
              (SELECT COALESCE(SUM(e.""Quantity""),0) FROM procurement.sorting_entries e
                 LEFT JOIN procurement.receipt_batches b ON b.""Id"" = e.""ReceiptBatchId""
                 WHERE NOT e.""IsDeleted"" AND e.""PutawayStatus"" = 'placed' AND e.""OnSaleAt"" IS NULL
                   AND (@sup IS NULL OR b.""SupplierId"" = @sup)),
              (SELECT COUNT(*)::int FROM procurement.missing_card_notices n WHERE NOT n.""IsDeleted"" AND n.""Status"" = 'open'),
              (SELECT EXTRACT(EPOCH FROM (now() - MIN(n.""CreatedAt"")))/86400.0
                 FROM procurement.missing_card_notices n WHERE NOT n.""IsDeleted"" AND n.""Status"" = 'open')";
        Kpis kpis;
        await using (var cmd = Cmd(conn, kpiSql, from, to, supplierId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            await r.ReadAsync(ct);
            kpis = new Kpis(
                r.IsDBNull(0) ? null : (double)r.GetDecimal(0),
                r.IsDBNull(1) ? null : (double)r.GetDecimal(1),
                r.GetInt32(2), r.GetDecimal(3), r.GetInt32(4), r.GetInt32(5), r.GetInt32(6),
                r.GetInt32(7), r.GetDecimal(8), r.GetInt32(9),
                r.IsDBNull(10) ? null : (double)r.GetDecimal(10));
        }

        // Satışa girmeyenler (yerleşti, OnSaleAt yok) — en eski 100
        const string nosSql = @"
            SELECT e.""Id"", e.""VariantId"", v.""ProductId"", p.""Code"",
                   COALESCE(p.""NameI18n""->>'tr', p.""Code""), v.""Sku"", e.""Quantity"", e.""PlacedAt""
            FROM procurement.sorting_entries e
            JOIN catalog.product_variants v ON v.""Id"" = e.""VariantId""
            JOIN catalog.products p ON p.""Id"" = v.""ProductId""
            LEFT JOIN procurement.receipt_batches b ON b.""Id"" = e.""ReceiptBatchId""
            WHERE NOT e.""IsDeleted"" AND e.""PutawayStatus"" = 'placed' AND e.""OnSaleAt"" IS NULL
              AND (@sup IS NULL OR b.""SupplierId"" = @sup)
            ORDER BY e.""PlacedAt"" LIMIT 100";
        var notOnSale = new List<NotOnSaleRow>();
        await using (var cmd = Cmd(conn, nosSql, from, to, supplierId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
                notOnSale.Add(new NotOnSaleRow(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetString(3),
                    r.GetString(4), r.GetString(5), r.GetDecimal(6), r.GetDateTime(7)));

        return (result, kpis, notOnSale);
    }

    private static NpgsqlCommand Cmd(NpgsqlConnection conn, string sql, DateTime from, DateTime to, Guid? sup)
    {
        var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 60 };
        cmd.Parameters.AddWithValue("from", from);
        cmd.Parameters.AddWithValue("to", to);
        cmd.Parameters.AddWithValue("sup", (object?)sup ?? DBNull.Value);
        return cmd;
    }
}
