using System.IO.Compression;
using ECSPros.Api.Grid;
using ECSPros.Api.Services.AiReporting;

namespace ECSPros.Api.Tests;

[TestClass]
public class ReportExcelExportTests
{
    [TestMethod]
    public void ExportBudgetCannotBeGrantedByClientJson()
    {
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        foreach (var json in new[] { "{\"exportMode\":true}", "{\"maxPageSize\":5000}" })
            Assert.ThrowsExactly<System.Text.Json.JsonException>(() => System.Text.Json.JsonSerializer.Deserialize<ReportGridState>(json, options));
        var permissions = new HashSet<string> { "reports.ai.use", "inventory.view" };
        var original = new ReportGridState(Page: 3, Search: "abc", Sort: "productCode");
        var export = ReportGridState.ForExport(original);
        Assert.AreEqual(1, export.Page);
        Assert.AreEqual(5000, export.PageSize);
        Assert.AreEqual(original.Search, export.Search);
        Assert.AreEqual(original.Sort, export.Sort);
        using var command = ReportGridQuery.Create(ReportGridQueryTests.Recipe, permissions, [], export);
        var restored = System.Text.Json.JsonSerializer.Deserialize<ReportGridState>(System.Text.Json.JsonSerializer.Serialize(export, options), options)!;
        Assert.ThrowsExactly<ArgumentException>(() => ReportGridQuery.Create(ReportGridQueryTests.Recipe, permissions, [], restored));
        Assert.ThrowsExactly<ArgumentException>(() => ReportGridQuery.Create(ReportGridQueryTests.Recipe, permissions, [], new(PageSize: 5000)));
    }

    [TestMethod]
    public void CompleteFiveThousandRowsAllowedButTextAndCountBudgetsAreEnforced()
    {
        var result = new StockReportResult(new[] { "x" }, Enumerable.Range(0, 5000).Select(i => new object?[] { i }).ToArray(), DateTimeOffset.UtcNow, 5000);
        ReportExcelExport.Validate(result);
        ReportExcelExport.CheckCount(5000);
        Assert.ThrowsExactly<InvalidOperationException>(() => ReportExcelExport.CheckCount(5001));
        Assert.ThrowsExactly<ArgumentException>(() => ReportExcelExport.Validate(result with
            { Rows = [new object?[] { new string('x', 32767) }], TotalCount = 1 }));
        Assert.ThrowsExactly<ArgumentException>(() => ReportExcelExport.Validate(result with
            { Rows = Enumerable.Range(0, 5000).Select(_ => new object?[] { new string('ş', 1000) }).ToArray() }));
    }

    [TestMethod]
    public void PartialUnknownAndOversizedResultsNeverBecomeFiles()
    {
        var result = new StockReportResult(new[] { "x" }, new[] { new object?[] { 1 } }, DateTimeOffset.UtcNow, 1);
        ReportExcelExport.Validate(result);
        Assert.ThrowsExactly<ArgumentException>(() => ReportExcelExport.Validate(result with { TotalCount = 2 }));
        Assert.ThrowsExactly<ArgumentException>(() => ReportExcelExport.Validate(result with { TotalCount = null }));
        Assert.ThrowsExactly<ArgumentException>(() => ReportExcelExport.Validate(result with { Rows = [], TotalCount = 0 }));
        Assert.ThrowsExactly<ArgumentException>(() => ReportExcelExport.Validate(result with { Columns = new[] { "x", "x" } }));
        Assert.ThrowsExactly<ArgumentException>(() => ReportExcelExport.Validate(result with { Rows = Enumerable.Range(0, ReportExcelExport.MaxRows + 1).Select(_ => new object?[] { 1 }).ToArray(), TotalCount = ReportExcelExport.MaxRows + 1 }));
    }
    [TestMethod]
    public async Task WorkbookUsesTypedNumbersNeutralizesFormulaTextAndCleansTempFile()
    {
        var result = new StockReportResult(new[] { "code", "amount", "currency" },
            new[] { new object?[] { "=1+1", -12.5m, "TRY" } }, DateTimeOffset.UtcNow, 1);
        ReportExcelExport.Validate(result);
        Assert.AreEqual(-12.5m, ReportExcelExport.Cell(-12.5m));
        Assert.AreEqual("'  @malicious", ReportExcelExport.Cell("  @malicious"));
        var stream = await GridExportWriter.WriteToTempAsync(result.Rows, ReportExcelExport.Columns(result), "Rapor", default);
        var file = stream.Name;
        try
        {
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var sheet = zip.GetEntry("xl/worksheets/sheet1.xml")!;
            using var reader = new StreamReader(sheet.Open());
            var xml = await reader.ReadToEndAsync();
            Assert.IsFalse(xml.Contains("<f>"));
            StringAssert.Contains(xml, "-12.5");
            StringAssert.Contains(xml, "TRY");
        }
        finally { await stream.DisposeAsync(); }
        Assert.IsFalse(File.Exists(file));
    }
}
