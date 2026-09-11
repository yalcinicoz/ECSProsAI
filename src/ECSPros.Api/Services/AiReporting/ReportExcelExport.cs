using ECSPros.Api.Grid;

namespace ECSPros.Api.Services.AiReporting;

public static class ReportExcelExport
{
    // Same snapshot/executor as the grid. Never stitch pages from different transactions.
    public const int MaxRows = 5000;
    public const int MaxTextBytes = 8 * 1024 * 1024;
    public static void CheckCount(long count)
    {
        if (count > MaxRows) throw new InvalidOperationException($"Excel satır sınırı {MaxRows}; filtreyi daraltın. Kısmi dosya oluşturulmadı.");
    }
    public static void Validate(StockReportResult result)
    {
        if (result.Rows.Count == 0) throw new ArgumentException("Aktarılacak sonuç yok.");
        if (result.TotalCount is null || result.TotalCount != result.Rows.Count || result.Rows.Count > MaxRows)
            throw new ArgumentException($"Excel aktarımı en fazla {MaxRows} filtrelenmiş sonuç içindir. Filtreyi daraltın; kısmi dosya üretilmedi.");
        if (result.Columns.Count == 0 || result.Columns.Distinct(StringComparer.Ordinal).Count() != result.Columns.Count
            || result.Rows.Any(row => row.Length != result.Columns.Count)) throw new ArgumentException("Rapor kolonları geçersiz.");
        long textBytes = 0;
        foreach (var value in result.Rows.SelectMany(row => row).OfType<string>())
        {
            // Excel has a per-cell character limit. Never silently truncate report content.
            if (value.Length > 32766 || (textBytes += System.Text.Encoding.UTF8.GetByteCount(value)) > MaxTextBytes)
                throw new ArgumentException("Excel metin sınırı aşıldı; daha dar filtre/kolon seçin. Kısmi dosya oluşturulmadı.");
        }
    }
    public static IReadOnlyList<GridExportColumn<object?[]>> Columns(StockReportResult result) => result.Columns
        .Select((id, index) => new GridExportColumn<object?[]>(id, id, row => Cell(row[index]))).ToArray();
    public static object? Cell(object? value) => value switch
    {
        // Extra defense for spreadsheet consumers; numeric negatives remain numeric.
        string text when text.TrimStart().StartsWith('=') || text.TrimStart().StartsWith('+')
            || text.TrimStart().StartsWith('-') || text.TrimStart().StartsWith('@') => "'" + text,
        DateTime time => GridExportWriter.ToIstanbul(time),
        DateTimeOffset time => GridExportWriter.ToIstanbul(time.UtcDateTime),
        _ => value
    };
}
