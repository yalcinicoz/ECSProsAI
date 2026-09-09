using System.Diagnostics;
using ECSPros.Iam.Application.Services;
using ECSPros.Iam.Domain.Entities;
using MiniExcelLibs;

namespace ECSPros.Api.Grid;

/// <summary>Export kolonu: anahtar (istemcinin görünür-kolon listesiyle eşleşir), Excel başlığı, satırdan değer.</summary>
/// <param name="AlanYetkisi">Y6 (K6): kolon hassas bir alansa gerekli yetki etiketi —
/// "cost" | "margin" | "phone" | "address" | "notes". Yetkisi olmayan kullanıcının
/// export'unda kolon HİÇ oluşmaz (tasarım §C.3: veri üretilmez).</param>
public sealed record GridExportColumn<TRow>(string Key, string Header, Func<TRow, object?> Value,
    bool Locked = false, string? AlanYetkisi = null);

/// <summary>
/// DataGrid Excel export (plan §2.8): aynı filtre modeliyle sayfalamasız sorgu → MiniExcel akışı (satır satır, bellek sabit).
/// Yazım geçici dosyaya yapılır (MiniExcel seekable akış ister), yanıt FileStream ile döner ve dosya kapanınca silinir.
/// </summary>
public static class GridExportWriter
{
    public const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private static readonly TimeZoneInfo TrTz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    /// <summary>İstenen kolon anahtarları (boş → tümü) + kilitli kolonlar; tanımdaki sıra korunur. Bilinmeyen anahtar → 400.</summary>
    public static IReadOnlyList<GridExportColumn<TRow>> Select<TRow>(IReadOnlyList<GridExportColumn<TRow>> all, IReadOnlyCollection<string>? requested)
    {
        if (requested is null || requested.Count == 0) return all;
        var set = new HashSet<string>(requested, StringComparer.OrdinalIgnoreCase);
        var unknown = set.FirstOrDefault(k => all.All(c => !c.Key.Equals(k, StringComparison.OrdinalIgnoreCase)));
        if (unknown is not null) throw new Shared.Kernel.Grid.GridException($"Geçersiz export kolonu: {unknown}");
        return all.Where(c => c.Locked || set.Contains(c.Key)).ToList();
    }

    /// <summary>Satırları geçici .xlsx dosyasına akıtır; açık FileStream (DeleteOnClose) döner — controller File(...) ile verir.</summary>
    public static async Task<FileStream> WriteToTempAsync<TRow>(IEnumerable<TRow> rows, IReadOnlyList<GridExportColumn<TRow>> columns,
        string sheetName, CancellationToken ct)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ecspros-export-{Guid.NewGuid():N}.xlsx");
        var fs = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1 << 16, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        try
        {
            await MiniExcel.SaveAsAsync(fs, Rows(rows, columns), sheetName: sheetName, cancellationToken: ct);
            fs.Position = 0;
            return fs;
        }
        catch { await fs.DisposeAsync(); throw; }
    }

    private static IEnumerable<IDictionary<string, object?>> Rows<TRow>(IEnumerable<TRow> rows, IReadOnlyList<GridExportColumn<TRow>> columns)
    {
        foreach (var r in rows)
        {
            var d = new Dictionary<string, object?>(columns.Count);
            foreach (var c in columns) d[c.Header] = c.Value(r);
            yield return d;
        }
    }

    public static DateTime ToIstanbul(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TrTz);
    public static string FileName(string prefix) => $"{prefix}-{ToIstanbul(DateTime.UtcNow):yyyy-MM-dd-HHmm}.xlsx";

    /// <summary>Kişisel veri çıkışı izi: iam.audit_logs (EntityType "grid_export"). Hata yutar — export'u düşürmez.</summary>
    public static async Task AuditAsync(IIamDbContext db, HttpContext http, string gridId, int rowCount, object filterOzet, Stopwatch sw, ILogger logger, CancellationToken ct)
    {
        try
        {
            db.AuditLogs.Add(new AuditLog
            {
                UserId = Guid.TryParse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    ?? http.User.FindFirst("sub")?.Value, out var uid) ? uid : null,
                EntityType = "grid_export",
                EntityId = Guid.Empty,
                Action = "export",
                NewValues = new Dictionary<string, object> { ["grid"] = gridId, ["rows"] = rowCount, ["ms"] = sw.ElapsedMilliseconds, ["filters"] = filterOzet },
                IpAddress = http.Connection.RemoteIpAddress?.ToString(),
                UserAgent = http.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua[..Math.Min(ua.Length, 500)] : null,
                Context = new Dictionary<string, object> { ["userName"] = http.User.FindFirst("full_name")?.Value ?? "" },
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) { logger.LogWarning(ex, "Grid export audit yazılamadı: {Grid}", gridId); }
    }
}
