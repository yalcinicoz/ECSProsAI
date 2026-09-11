using ECSPros.Api.Services.AiReporting;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class ReportRelativePeriodTests
{
    [TestMethod]
    public void TurkeyCalendarHandlesYearBoundaryAndLeapMonth()
    {
        // Already January in Turkey, still December in UTC.
        var january = ReportRelativePeriod.Resolve("lastMonth", DateTimeOffset.Parse("2025-12-31T22:00:00Z"));
        Assert.AreEqual(DateTimeOffset.Parse("2025-11-30T21:00:00Z"), january.From);
        Assert.AreEqual(DateTimeOffset.Parse("2025-12-31T21:00:00Z"), january.To);
        var leap = ReportRelativePeriod.Resolve("lastMonth", DateTimeOffset.Parse("2024-03-15T00:00:00Z"));
        Assert.AreEqual(TimeSpan.FromDays(29), leap.To - leap.From);
        var days = ReportRelativePeriod.Resolve("last30Days", DateTimeOffset.Parse("2026-09-11T12:00:00Z"));
        Assert.AreEqual(TimeSpan.FromDays(30), days.To - days.From);
        Assert.AreEqual(DateTimeOffset.Parse("2026-09-11T21:00:00Z"), days.To);
    }
    [TestMethod]
    public void RelativeChoiceIsExplicitAndUnknownPeriodFailsClosed()
    {
        const string json = """{"version":2,"source":"orders","from":"2020-01-01T00:00:00Z","to":"2020-02-01T00:00:00Z","aggregate":{"dimensions":[],"measures":["orders.count"]}}""";
        var fixedPlan = DynamicReportPlan.Parse(json);
        Assert.AreEqual(2020, fixedPlan.Scope().From.Year);
        var relative = fixedPlan with { Period = "last6Months" };
        var period = relative.Scope();
        Assert.IsTrue(period.To - period.From <= TimeSpan.FromDays(184));
        Assert.ThrowsExactly<ArgumentException>(() => (fixedPlan with { Period = "allHistory" }).Scope());
        Assert.ThrowsExactly<ArgumentException>(() => (relative with { From = "invalid" }).Scope());
        Assert.ThrowsExactly<ArgumentException>(() => (relative with { To = fixedPlan.From }).Scope());
        Assert.ThrowsExactly<ArgumentException>(() => ReportRelativePeriod.Resolve("", DateTimeOffset.UtcNow));
        // Additional absolute predicates are intentionally not rewritten by a root-period choice.
        var predicate = new ReportPredicate { Kind = "compare", Field = "orders.createdAt", Operator = "gte", Values = ["2020-01-01T00:00:00Z"] };
        Assert.AreSame(predicate, (relative with { Predicate = predicate }).Predicate);
    }
}
