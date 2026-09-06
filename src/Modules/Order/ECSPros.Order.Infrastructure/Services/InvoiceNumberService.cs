using ECSPros.Order.Application.Services;
using ECSPros.Order.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Infrastructure.Services;

/// <summary>
/// Seri × yıl sayacından atomik fatura numarası (FE0 §2.5). Tek INSERT … ON CONFLICT DO UPDATE … RETURNING:
/// satır yoksa 1 ile açılır, varsa satır kilidi altında +1 — eşzamanlı istekler sıralanır, aynı numara
/// iki kez verilmez. Çağıran açık transaction içinde fatura satırını yazıp commit eder; commit edilmezse
/// sayaç artışı da geri alınır (boşluk yok). Tarih-sıra kuralı FE1 (K3): LastInvoiceDate burada güncellenir.
/// </summary>
public class InvoiceNumberService(OrderDbContext db) : IInvoiceNumberService
{
    private sealed class Slot { public int Value { get; set; } }

    public async Task<InvoiceNumberAllocation> AllocateAsync(Guid seriesId, string serial, DateTime invoiceDateUtc, CancellationToken ct = default)
    {
        var year = invoiceDateUtc.Year.ToString();
        var rows = await db.Database.SqlQuery<Slot>($"""
            WITH taken AS (
                INSERT INTO "order".ord_invoice_series_counters
                    ("Id", "InvoiceSeriesId", "Year", "LastSequence", "LastInvoiceDate", "CreatedAt", "IsDeleted")
                VALUES ({Guid.NewGuid()}, {seriesId}, {year}, 1, {invoiceDateUtc}, timezone('utc', now()), false)
                ON CONFLICT ("InvoiceSeriesId", "Year") DO UPDATE
                    SET "LastSequence" = ord_invoice_series_counters."LastSequence" + 1,
                        "LastInvoiceDate" = GREATEST(ord_invoice_series_counters."LastInvoiceDate", EXCLUDED."LastInvoiceDate"),
                        "UpdatedAt" = timezone('utc', now())
                RETURNING "LastSequence" AS "Value"
            )
            SELECT "Value" FROM taken
            """).ToListAsync(ct);

        var sequence = rows.Single().Value;
        return new InvoiceNumberAllocation(serial, year, sequence, $"{serial}{year}{sequence:D9}");
    }
}
