using ECSPros.Api.Services.ErpSource;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class ErpProductGroupDefaultTests
{
    [TestMethod]
    public void CanonicalSourceCode_UsesOnlyExactPanelMapping_AndRejectsConflicts()
    {
        var id = Guid.NewGuid();
        var mappings = new Dictionary<string, List<Guid>>(StringComparer.Ordinal) { ["AM"] = [id] };
        Assert.AreEqual(id, ErpPanelGroupResolver.ResolveCode(" AM ", mappings));
        Assert.IsNull(ErpPanelGroupResolver.ResolveCode("am", mappings));
        Assert.IsNull(ErpPanelGroupResolver.ResolveCode("00", mappings));
        mappings["AM"].Add(Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => ErpPanelGroupResolver.ResolveCode("AM", mappings));
    }

    [TestMethod]
    public void PanelMode_RejectsErpAttributeOverrideOfGroupClassification()
    {
        var options = new ErpSourceOptions { UsePanelGroupMappings = true };
        options.ProductAttributeTypeCodes["999"] = "urun_grubu";
        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [TestMethod]
    public void DefaultInsert_IsScopedToOneExistingActiveDefinition()
    {
        var sql = ErpProductGroupDefault.InsertMissingSql;
        StringAssert.Contains(sql, "t.\"Code\"='urun_grubu'");
        StringAssert.Contains(sql, "v.\"AttributeTypeId\"=t.\"Id\"");
        StringAssert.Contains(sql, "(SELECT count(*) FROM candidate)=1");
        StringAssert.Contains(sql, "p.\"Id\"=@product");
        StringAssert.Contains(sql, "g.\"Code\"<>'gecici'");
        StringAssert.Contains(sql, "v.\"IsActive\" AND NOT v.\"IsDeleted\"");
    }

    [TestMethod]
    public void DefaultInsert_PreservesExistingAndSoftDeletedValues_AndIsIdempotent()
    {
        var sql = ErpProductGroupDefault.InsertMissingSql;
        var guard = sql[(sql.IndexOf("AND NOT EXISTS", StringComparison.Ordinal))..];
        StringAssert.Contains(guard, "existing.\"ProductId\"=@product");
        StringAssert.Contains(guard, "existing.\"AttributeTypeId\"=c.\"AttributeTypeId\"");
        Assert.IsFalse(guard.Contains("IsDeleted", StringComparison.Ordinal));
        StringAssert.Contains(guard, "DO NOTHING");
        Assert.IsFalse(sql.Contains("UPDATE ", StringComparison.Ordinal));
        Assert.IsFalse(sql.Contains("DELETE ", StringComparison.Ordinal));
        Assert.IsFalse(sql.Contains("INSERT INTO definition", StringComparison.Ordinal));
    }
}
