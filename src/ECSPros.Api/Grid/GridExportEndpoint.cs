using System.Diagnostics;
using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Grid;

/// <summary>
/// DataGrid export ucu ortak akışı (F4): tavan → MiniExcel geçici dosya → audit → File. Controller yalnız kaynağı ve kolonları verir.
/// Kullanım: <c>return await GridExportEndpoint.RunAsync(this, body, cfg, iam, logger, "orders", "siparisler", "Siparişler", OrderExportColumns.All, max => mediator.Send(new ExportXQuery(..., max)), ct);</c>
/// </summary>
public static class GridExportEndpoint
{
    public static async Task<IActionResult> RunAsync<TRow>(
        ControllerBase controller, GridExportRequest body, IConfiguration config, IIamDbContext iam, ILogger logger,
        string gridId, string filePrefix, string sheetName, IReadOnlyList<GridExportColumn<TRow>> allColumns,
        Func<int, Task<Result<GridExportSource<TRow>>>> source, CancellationToken ct)
    {
        var grid = body.ToGridRequest();
        var sw = Stopwatch.StartNew();
        var max = config.GetValue("Grid:ExportMaxRows", 100_000);
        var src = await source(max);
        if (src.IsFailure) return controller.BadRequest(new { success = false, error = src.Error });
        var cols = GridExportWriter.Select(allColumns, body.Columns);
        var file = await GridExportWriter.WriteToTempAsync(src.Value!.Rows.AsEnumerable(), cols, sheetName, ct);
        await GridExportWriter.AuditAsync(iam, controller.HttpContext, gridId, src.Value.Count,
            new { grid.Search, grid.Sort, grid.Dir, filters = grid.Filters.Select(f => $"{f.Field} {f.Op} {f.Value}").ToList(), named = body.Named, columns = cols.Select(c => c.Key).ToList() },
            sw, logger, ct);
        return controller.File(file, GridExportWriter.XlsxMime, GridExportWriter.FileName(filePrefix));
    }

    /// <summary>Export tavanı mesajı (handler'lar aynı metni kullansın).</summary>
    public static string TavanMesaji(int count, int max) => $"Sonuç {count:N0} satır; dışa aktarma sınırı {max:N0}. Filtreyi daraltın.";

    // named sözlüğü yardımcıları
    public static DateTime? Tarih(GridExportRequest b, string key) => DateTime.TryParse(b.NamedValue(key), System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var d) ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : null;
    public static Guid? Kimlik(GridExportRequest b, string key) => Guid.TryParse(b.NamedValue(key), out var g) ? g : null;
    public static bool? Bayrak(GridExportRequest b, string key) => bool.TryParse(b.NamedValue(key), out var x) ? x : null;
    public static int? Sayi(GridExportRequest b, string key) => int.TryParse(b.NamedValue(key), out var x) ? x : null;
    public static List<string>? Liste(GridExportRequest b, string key) => b.NamedValue(key) is { } s
        ? s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() : null;
}
