using System.Text.Json;
using ECSPros.Api.Services.AiReporting;
using ECSPros.Order.Infrastructure.Persistence;
using ECSPros.Shared.Kernel.Authorization;
using Microsoft.EntityFrameworkCore;
using OrderEntity = ECSPros.Order.Domain.Entities.Order;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class ReportCustomerIdentityTests
{
    private static readonly HashSet<string> Base = new() { "reports.ai.use", "orders.view" };
    private static readonly HashSet<string> Allowed = new(Base) { "crm.members.view" };
    private static ReportPredicate Members() => new() { Kind = "compare", Field = "orders.memberId", Operator = "isNotNull" };
    private static ReportAggregatePlan Summary() => new() { Dimensions = ["orders.memberId"], Measures = ["orders.count"], Sort = "orders.count", Direction = "desc", Top = 20 };
    [TestMethod]
    public void CustomerFieldsRequireCrmForMetadataPredicatesDetailsAndAggregates()
    {
        var dictionary = ReportBusinessDictionary.Orders;
        Assert.IsFalse(dictionary.Describe(Base).Any(f => f.Id == "orders.memberId" || f.Id == "orders.memberCount"));
        Assert.IsTrue(dictionary.Describe(Allowed).Any(f => f.Id == "orders.memberId"));
        var rows = Array.Empty<OrderEntity>().AsQueryable();
        var predicates = dictionary.Predicates(_ => true);
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => predicates.Apply(rows, Members(), Base));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => predicates.Apply(rows, new() { Kind = "not", Children = [Members()] }, Base));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => dictionary.Aggregates(_ => true).Build(rows, Summary(), Base));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => dictionary.Aggregates(_ => true).Build(rows, new() { Dimensions = [], Measures = ["orders.memberCount"] }, Base));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => dictionary.Details(_ => true, o => o.Id).Build(rows, new() { Columns = ["orders.memberId"] }, new(), Base));
        var scoped = new EfektifYetkiler(false, new() { ["reports.ai.use"] = null, ["orders.view"] = null, ["crm.members.view"] = [Guid.NewGuid()] });
        Assert.IsFalse(StockReportExecutor.ResolvePermissions(scoped).Contains("crm.members.view"));
    }
    [TestMethod]
    public void RankedIdentityAndDistinctCountExcludeGuestsWithoutInventingIdentity()
    {
        var member = Guid.NewGuid();
        var rows = new[] { new OrderEntity { MemberId = member }, new OrderEntity { MemberId = member }, new OrderEntity { MemberId = null }, new OrderEntity { MemberId = Guid.NewGuid(), IsDeleted = true } }.AsQueryable();
        var dictionary = ReportBusinessDictionary.Orders;
        var selected = dictionary.Predicates(o => !o.IsDeleted).Apply(rows, Members(), Allowed);
        Assert.AreEqual(2, selected.Count());
        var result = dictionary.Aggregates(o => !o.IsDeleted).Build(selected, Summary(), Allowed).Rows.Single();
        Assert.AreEqual(member.ToString(), result.D0);
        Assert.AreEqual(2m, result.M0);
        var unique = dictionary.Aggregates(o => !o.IsDeleted).Build(rows, new() { Dimensions = [], Measures = ["orders.memberCount"] }, Allowed).Rows.Single();
        Assert.AreEqual(1m, unique.M0);
        Assert.ThrowsExactly<ArgumentException>(() => dictionary.Predicates(_ => true).Apply(rows, Members() with { Values = ["x"] }, Allowed));
    }
    [TestMethod]
    public void PostgreSqlTranslationAndAuthorizedAiSchemaUseSameFields()
    {
        using var db = new OrderDbContext(new DbContextOptionsBuilder<OrderDbContext>().UseNpgsql("Host=localhost;Database=not_opened;Username=unused").Options);
        var dictionary = ReportBusinessDictionary.Orders;
        var filtered = dictionary.Predicates(o => !o.IsDeleted).Apply(db.Orders, Members(), Allowed);
        var sql = dictionary.Aggregates(o => !o.IsDeleted).Build(filtered, Summary(), Allowed).Rows.ToQueryString();
        StringAssert.Contains(sql, "MemberId");
        StringAssert.Contains(sql, "IS NOT NULL");
        StringAssert.Contains(sql, "LIMIT");
        using var basic = JsonDocument.Parse(OpenAiDynamicReportContract.CreateRequest("configured-model", "rapor", Base, null));
        using var allowed = JsonDocument.Parse(OpenAiDynamicReportContract.CreateRequest("configured-model", "rapor", Allowed, null));
        var path = (JsonDocument doc) => doc.RootElement.GetProperty("text").GetProperty("format").GetProperty("schema").GetRawText();
        Assert.IsFalse(path(basic).Contains("orders.memberId"));
        Assert.IsTrue(path(allowed).Contains("orders.memberId"));
        Assert.IsTrue(path(allowed).Contains("isNotNull"));
    }
}
