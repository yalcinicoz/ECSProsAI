using ECSPros.Api.Services.ErpSource;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class ErpOrtamPriorityTests
{
    [TestMethod]
    public void Tesettur_ReplacesAllOrtamValues_PreservesOtherAttributes()
    {
        ErpProductAttributeRow[] source = [new("55", "Günlük", "Ortam", "10"),
            new("55", "Business", "Ortam", "01"), new("20", "Normal Kalıp", "Kalıp", "1")];
        var result = ErpOrtamPriority.Apply(source, true);
        Assert.HasCount(2, result);
        Assert.AreEqual(source[2], result[0]);
        Assert.AreEqual("TESETTÜR", result[1].Value);
        Assert.AreEqual("55", result[1].KeywordId);
        Assert.IsNull(result[1].SourceCode);
        Assert.IsTrue(result[1].UseExistingDefinitionOnly);
        Assert.HasCount(3, source);
    }

    [TestMethod]
    public void Unmarked_PreservesV3Ortam()
    {
        ErpProductAttributeRow[] source = [new("55", "Günlük", "Ortam", "10")];
        Assert.AreSame(source, ErpOrtamPriority.Apply(source, false));
    }

    [TestMethod]
    public void MarkedWithoutOtherAttributes_StillGetsOrtam()
    {
        var result = ErpOrtamPriority.Apply([], true);
        Assert.HasCount(1, result);
        Assert.AreEqual("TESETTÜR", result[0].Value);
    }

    [TestMethod]
    public void RepeatedRefresh_IsIdempotent()
    {
        var first = ErpOrtamPriority.Apply([], true);
        CollectionAssert.AreEqual(first.ToArray(), ErpOrtamPriority.Apply(first, true).ToArray());
    }

    [TestMethod]
    public void RemovedMarker_UsesFreshV3Ortam_NotPreviousOverride()
    {
        ErpProductAttributeRow[] fresh = [new("55", "Günlük", "Ortam", "10")];
        var marked = ErpOrtamPriority.Apply(fresh, true);
        Assert.AreEqual("TESETTÜR", marked.Single().Value);
        var refreshed = ErpOrtamPriority.Apply(fresh, false);
        Assert.AreEqual("Günlük", refreshed.Single().Value);
        Assert.IsFalse(refreshed.Single().UseExistingDefinitionOnly);
    }
}
