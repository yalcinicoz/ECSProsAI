using ECSPros.Api.Grid;
using ECSPros.Shared.Kernel.Grid;
using MiniExcelLibs;

namespace ECSPros.Api.Tests;

/// <summary>DataGrid F0: MiniExcel akış yazıcısı + kolon seçimi (kilitli kolonlar, bilinmeyen anahtar).</summary>
[TestClass]
public sealed class GridExportWriterTests
{
    private sealed record R(string No, string Durum, decimal Tutar, DateTime Tarih);

    private static readonly IReadOnlyList<GridExportColumn<R>> Cols = new GridExportColumn<R>[]
    {
        new("no", "Sipariş No", r => r.No, Locked: true),
        new("status", "Durum", r => r.Durum),
        new("total", "Tutar", r => r.Tutar),
        new("createdAt", "Tarih", r => r.Tarih),
    };

    [TestMethod]
    public void Select_keeps_definition_order_adds_locked_and_rejects_unknown()
    {
        var sel = GridExportWriter.Select(Cols, new[] { "createdAt", "total" });
        CollectionAssert.AreEqual(new[] { "no", "total", "createdAt" }, sel.Select(c => c.Key).ToList());
        Assert.AreEqual(4, GridExportWriter.Select(Cols, null).Count);
        Assert.AreEqual(4, GridExportWriter.Select(Cols, Array.Empty<string>()).Count);
        Assert.ThrowsExactly<GridException>(() => GridExportWriter.Select(Cols, new[] { "password" }));
    }

    [TestMethod]
    public async Task Writes_all_rows_streaming_and_enumerates_source_once()
    {
        var enumerations = 0;
        IEnumerable<R> Source()
        {
            enumerations++;
            for (var i = 1; i <= 12_000; i++)
                yield return new R($"MIS{i:D7}", i % 2 == 0 ? "Kargoda" : "Bekleyen", i * 1.5m, new DateTime(2026, 9, 8, 10, 0, 0, DateTimeKind.Utc).AddMinutes(i));
        }
        await using var fs = await GridExportWriter.WriteToTempAsync(Source(), Cols, "Siparişler", CancellationToken.None);
        Assert.IsTrue(fs.Length > 10_000, "dosya yazılmalı");
        var rows = MiniExcel.Query(fs, useHeaderRow: true, sheetName: "Siparişler").ToList();
        Assert.AreEqual(12_000, rows.Count);
        var first = (IDictionary<string, object>)rows[0];
        CollectionAssert.AreEquivalent(new[] { "Sipariş No", "Durum", "Tutar", "Tarih" }, first.Keys.ToList());
        Assert.AreEqual("MIS0000001", first["Sipariş No"]);
        Assert.AreEqual(1.5d, Convert.ToDouble(first["Tutar"]), 0.0001, "sayısal hücre (Excel'de toplanabilir)");
        Assert.AreEqual(1, enumerations, "kaynak yalnız bir kez okunmalı (DB sorgusu iki kez koşmasın)");
        var path = fs.Name; await fs.DisposeAsync();
        Assert.IsFalse(File.Exists(path), "DeleteOnClose: geçici dosya silinmeli");
    }

    [TestMethod]
    public void FileName_uses_istanbul_time_and_xlsx()
    {
        var n = GridExportWriter.FileName("siparisler");
        StringAssert.StartsWith(n, "siparisler-20");
        StringAssert.EndsWith(n, ".xlsx");
        Assert.AreEqual(3, GridExportWriter.ToIstanbul(new DateTime(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc)).Hour);
    }
}
