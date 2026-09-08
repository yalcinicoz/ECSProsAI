using ECSPros.Api.Grid;
using ECSPros.Shared.Kernel.Grid;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace ECSPros.Api.Tests;

/// <summary>DataGrid F0 (docs/datagrid-standardi-plani.md): beyaz listeli filtre/sıralama motoru + query string ayrıştırıcı.</summary>
[TestClass]
public sealed class GridSchemaTests
{
    private sealed record Row(Guid Id, string Number, string? Customer, string Status, string? Method, DateTime CreatedAt, decimal Total, bool Paid, Guid? MemberId);

    private static readonly Guid M1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly List<Row> Rows = new()
    {
        new(Guid.Parse("00000000-0000-0000-0000-000000000001"), "MIS0001", "Ahmet Yılmaz", "pending",   "kart",        new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc), 100m, false, M1),
        new(Guid.Parse("00000000-0000-0000-0000-000000000002"), "MIS0002", "Ayşe Demir",   "confirmed", null,          new DateTime(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc), 1500m, true, null),
        new(Guid.Parse("00000000-0000-0000-0000-000000000003"), "MIS0003", null,           "shipped",   "kapida-nakit", new DateTime(2026, 9, 8, 20, 30, 0, DateTimeKind.Utc), 250.5m, true, M1),
        new(Guid.Parse("00000000-0000-0000-0000-000000000004"), "MIS0004", "ahmet kaya",   "cancelled", "kart",        new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc), 1500m, false, null),
    };

    private static readonly GridSchema<Row> Schema = new GridSchema<Row>()
        .Text("customer", r => r.Customer)
        .Text("number", r => r.Number)
        .Enum("status", r => r.Status, new[] { "pending", "confirmed", "shipped", "cancelled" })
        .Enum("method", r => r.Method, new[] { "kart", "kapida-nakit", "none" }, nullToken: "none")
        .Date("createdAt", r => r.CreatedAt)
        .Number("total", r => r.Total)
        .Bool("paid", r => r.Paid)
        .Guid("memberId", r => r.MemberId)
        .Sort("createdAt", r => r.CreatedAt).Sort("total", r => r.Total).Sort("number", r => r.Number)
        .DefaultSort(r => r.CreatedAt, desc: true).TieBreaker(r => r.Id);

    private static GridRequest Req(params (string f, string op, string v)[] filters) =>
        new GridRequest { Filters = filters.Select(x => new GridFilter(x.f, x.op, x.v)).ToList() };

    private static List<string> Apply(GridRequest r) => Schema.ApplySort(Schema.ApplyFilters(Rows.AsQueryable(), r), r).Select(x => x.Number).ToList();

    [TestMethod]
    public void Text_contains_is_case_insensitive_and_null_safe()
    {
        CollectionAssert.AreEquivalent(new[] { "MIS0001", "MIS0004" }, Apply(Req(("customer", "contains", "AHMET"))));
        CollectionAssert.AreEquivalent(new[] { "MIS0001" }, Apply(Req(("customer", "startswith", "ahmet y"))));
        CollectionAssert.AreEquivalent(new[] { "MIS0002" }, Apply(Req(("customer", "eq", "ayşe demir"))));
        CollectionAssert.AreEquivalent(new[] { "MIS0001", "MIS0004" }, Apply(Req(("customer", "auto", "ahmet"))), "auto → contains");
        Assert.AreEqual(4, Apply(Req(("customer", "contains", "  "))).Count, "boş değer = filtre yok");
    }

    [TestMethod]
    public void Enum_in_with_null_token_and_validation()
    {
        CollectionAssert.AreEquivalent(new[] { "MIS0001", "MIS0003" }, Apply(Req(("status", "in", "pending,shipped"))));
        CollectionAssert.AreEquivalent(new[] { "MIS0002", "MIS0003" }, Apply(Req(("method", "in", "none,kapida-nakit"))), "none → NULL");
        CollectionAssert.AreEquivalent(new[] { "MIS0002" }, Apply(Req(("method", "eq", "none"))));
        Assert.ThrowsExactly<GridException>(() => Apply(Req(("status", "in", "pending,bilinmeyen"))));
        Assert.ThrowsExactly<GridException>(() => Apply(Req(("status", "eq", "pending,shipped"))));
    }

    [TestMethod]
    public void Date_between_day_only_is_istanbul_inclusive_and_iso_is_exclusive()
    {
        // 2026-09-01..2026-09-08 (İstanbul günleri, bitiş dahil): 1, 5 ve 8 Eylül 20:30Z (=23:30 İstanbul) dahil; 20 Ağustos hariç
        CollectionAssert.AreEquivalent(new[] { "MIS0001", "MIS0002", "MIS0003" }, Apply(Req(("createdAt", "between", "2026-09-01,2026-09-08"))));
        // ISO bitiş exclusive
        CollectionAssert.AreEquivalent(new[] { "MIS0001" }, Apply(Req(("createdAt", "between", "2026-09-01T00:00:00Z,2026-09-05T12:00:00Z"))));
        CollectionAssert.AreEquivalent(new[] { "MIS0003" }, Apply(Req(("createdAt", "gte", "2026-09-08"))));
        Assert.ThrowsExactly<GridException>(() => Apply(Req(("createdAt", "between", "2026-09-08,2026-09-01"))));
        Assert.ThrowsExactly<GridException>(() => Apply(Req(("createdAt", "between", "dün,bugün"))));
    }

    private sealed record DayRow(Guid Id, DateOnly InvoiceDate, DateOnly? DueDate);
    private static readonly List<DayRow> DayRows = new()
    {
        new(Guid.Parse("00000000-0000-0000-0000-00000000000a"), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)),
        new(Guid.Parse("00000000-0000-0000-0000-00000000000b"), new DateOnly(2026, 9, 8), null),
        new(Guid.Parse("00000000-0000-0000-0000-00000000000c"), new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 15)),
    };
    private static readonly GridSchema<DayRow> DaySchema = new GridSchema<DayRow>()
        .Date("invoiceDate", r => r.InvoiceDate)
        .Date("dueDate", r => r.DueDate)
        .Sort("dueDate", r => r.DueDate).DefaultSort(r => r.InvoiceDate).TieBreaker(r => r.Id);

    [TestMethod]
    public void DateOnly_between_is_inclusive_and_operators_compare_days()
    {
        static List<DayRow> F(string field, string op, string value) =>
            DaySchema.ApplyFilters(DayRows.AsQueryable(), new GridRequest { Filters = new() { new(field, op, value) } }).ToList();
        Assert.AreEqual(2, F("invoiceDate", "between", "2026-09-01,2026-09-08").Count);           // iki uç dahil
        Assert.AreEqual(1, F("invoiceDate", "between", "2026-09-02,2026-09-30").Count);
        Assert.AreEqual(1, F("invoiceDate", "eq", "2026-09-08").Count);
        Assert.AreEqual(2, F("invoiceDate", "gte", "2026-09-08").Count);
        Assert.AreEqual(1, F("invoiceDate", "gt", "2026-09-08").Count);
        Assert.AreEqual(1, F("invoiceDate", "lt", "2026-09-08").Count);
        Assert.AreEqual(1, F("dueDate", "lte", "2026-09-30").Count);                                   // null vade eşleşmez
        Assert.AreEqual(1, F("invoiceDate", "auto", "2026-09-08T10:00:00Z,2026-09-08T23:00:00Z").Count); // ISO → İstanbul günü
        Assert.ThrowsExactly<GridException>(() => F("invoiceDate", "between", "2026-09-08,2026-09-01"));
        Assert.ThrowsExactly<GridException>(() => F("invoiceDate", "eq", "dün"));
    }

    [TestMethod]
    public void Number_bool_guid_operators()
    {
        CollectionAssert.AreEquivalent(new[] { "MIS0002", "MIS0004" }, Apply(Req(("total", "gt", "1000"))));
        CollectionAssert.AreEquivalent(new[] { "MIS0001", "MIS0003" }, Apply(Req(("total", "between", "100;300"))));
        CollectionAssert.AreEquivalent(new[] { "MIS0003" }, Apply(Req(("total", "eq", "250,5"))), "virgül ondalık kabul");
        CollectionAssert.AreEquivalent(new[] { "MIS0002", "MIS0003" }, Apply(Req(("paid", "eq", "true"))));
        CollectionAssert.AreEquivalent(new[] { "MIS0001", "MIS0004" }, Apply(Req(("paid", "auto", "false"))));
        CollectionAssert.AreEquivalent(new[] { "MIS0001", "MIS0003" }, Apply(Req(("memberId", "eq", M1.ToString()))));
        Assert.ThrowsExactly<GridException>(() => Apply(Req(("total", "gt", "abc"))));
        Assert.ThrowsExactly<GridException>(() => Apply(Req(("memberId", "eq", "x"))));
    }

    [TestMethod]
    public void Unknown_field_or_sort_is_rejected_and_skip_fields_work()
    {
        Assert.ThrowsExactly<GridException>(() => Apply(Req(("hackerField", "eq", "1"))));
        Assert.ThrowsExactly<GridException>(() => Apply(new GridRequest { Sort = "Password" }));
        // sekme sayaçları: status filtresi atlanır
        var q = Schema.ApplyFilters(Rows.AsQueryable(), Req(("status", "in", "pending"), ("paid", "eq", "true")), "status");
        Assert.AreEqual(2, q.Count());
    }

    [TestMethod]
    public void Sort_default_desc_with_tiebreaker_and_explicit_asc()
    {
        CollectionAssert.AreEqual(new[] { "MIS0003", "MIS0002", "MIS0001", "MIS0004" }, Apply(new GridRequest()));
        CollectionAssert.AreEqual(new[] { "MIS0004", "MIS0001", "MIS0002", "MIS0003" }, Apply(new GridRequest { Sort = "createdAt", Dir = "asc" }));
        // eşit toplamda (1500) tie-breaker Id: desc → 4 önce 2
        var byTotal = Apply(new GridRequest { Sort = "total", Dir = "desc" });
        CollectionAssert.AreEqual(new[] { "MIS0004", "MIS0002", "MIS0003", "MIS0001" }, byTotal);
    }

    [TestMethod]
    public void Parser_reads_page_sort_and_f_filters_with_clamp()
    {
        var q = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["page"] = "0", ["pageSize"] = "9999", ["search"] = " ahmet ", ["sort"] = "createdAt", ["dir"] = "ASC",
            ["f.status"] = "in:pending,confirmed", ["f.total"] = "gt:1000", ["f.customer"] = "ahmet",
            ["f.createdAt"] = "2026-09-01T00:00:00Z", ["fq.createdAt"] = "last7", ["f."] = "x",
        });
        var r = GridRequestParser.Parse(q, defaultPageSize: 20);
        Assert.AreEqual(1, r.Page);
        Assert.AreEqual(GridRequest.MaxPageSize, r.PageSize);
        Assert.AreEqual("ahmet", r.Search);
        Assert.AreEqual("createdAt", r.Sort); Assert.AreEqual("asc", r.Dir); Assert.IsFalse(r.Desc!.Value);
        Assert.AreEqual(4, r.Filters.Count);
        Assert.AreEqual("in", r.Filters.Single(f => f.Field == "status").Op);
        Assert.AreEqual("auto", r.Filters.Single(f => f.Field == "customer").Op);
        var tarih = r.Filters.Single(f => f.Field == "createdAt");
        Assert.AreEqual("auto", tarih.Op, "ISO değerdeki ':' operatör sanılmamalı");
        Assert.AreEqual("2026-09-01T00:00:00Z", tarih.Value);
        var bos = GridRequestParser.Parse(new QueryCollection(), 20);
        Assert.AreEqual(20, bos.PageSize); Assert.IsTrue(bos.IsEmpty);
    }

    [TestMethod]
    public void Export_request_maps_to_grid_request_without_paging()
    {
        var e = new GridExportRequest { Search = "x", Sort = "total", Dir = "desc", Filters = new() { new("status", "in", "pending") }, Named = new() { ["from"] = "2026-09-01" } };
        var g = e.ToGridRequest();
        Assert.AreEqual(1, g.Page); Assert.AreEqual(GridRequest.MaxPageSize, g.PageSize);
        Assert.AreEqual("2026-09-01", e.NamedValue("from")); Assert.IsNull(e.NamedValue("to"));
        Assert.AreEqual(0, g.Without("status").Filters.Count);
    }
}
