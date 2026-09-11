using ECSPros.Api.Services.AiReporting;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class ReportBindingGuardTests
{
    private static ReportField Attribute(string label, params string[] values) =>
        ReportAttributeCatalog.Field(Guid.NewGuid(), label, "test") with { MatchedValues = values };

    [TestMethod]
    public void UnknownGroupMatchingAttributeRequiresClarificationWithoutHardcodedNames()
    {
        var fields = new[] { Attribute("Ortam", "TESETTÜR"), Attribute("Kullanım Tipi", "Outdoor") };
        StringAssert.Contains(ReportBindingGuard.Question(["tesettur"], [], fields)!, "Ortam");
        StringAssert.Contains(ReportBindingGuard.Question(["outdoor"], [], fields)!, "Kullanım Tipi");
        Assert.IsNull(ReportBindingGuard.Question(["olmayan"], [], fields));
        Assert.IsNull(ReportBindingGuard.Question(["tesettur"], ["Tesettür"], fields));
        Assert.IsNull(ReportBindingGuard.Question(["outdoor"], [], []));
    }

    [TestMethod]
    public void ChecksExactSetMembershipNotSubstringAndSupportsTurkishSpelling()
    {
        Assert.IsNull(ReportBindingGuard.Question(["tesettur"], [], [Attribute("Ortam", "Tesettür değil")]));
        Assert.AreEqual(ReportBindingGuard.Fold(" İÇ GİYİM "), ReportBindingGuard.Fold("ic giyim"));
        Assert.IsNotNull(ReportBindingGuard.Question(["İÇ GİYİM"], [], [Attribute("Ürün Grubu", "İç Giyim")]));
    }

    [TestMethod]
    public void NestedConditionsAreInspectedButOtherSourcesAndFieldsStayUnchanged()
    {
        var plan = new DynamicReportPlan { Source = "stock", Predicate = new ReportPredicate { Kind = "all", Children = [
            new() { Kind = "compare", Field = "productGroup", Operator = "in", Values = ["Outdoor", "Tesettür"] },
            new() { Kind = "compare", Field = "productCode", Operator = "eq", Values = ["P-1"] }
        ] } };
        CollectionAssert.AreEquivalent(new[] { "Outdoor", "Tesettür" }, ReportBindingGuard.ProductGroupValues(plan).ToArray());
        Assert.AreEqual(0, ReportBindingGuard.ProductGroupValues(plan with { Source = "orders" }).Count);
    }
}
