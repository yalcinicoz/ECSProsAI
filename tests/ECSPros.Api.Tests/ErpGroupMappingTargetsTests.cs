using System.Text.Json;
using ECSPros.Api.Services.ErpSource;
using ECSPros.Api.Services.Marketplace.Mapping;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class ErpGroupMappingTargetsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [TestMethod]
    public void Direct_Rules_AndPool_ExposeAllReferencedCodes()
    {
        CollectionAssert.AreEqual(new[] { "A" }, ErpGroupMappingTargets.Read("direct", "A", null, null));
        var rule = new MappingRuleDto(0, "cinsiyet", Guid.NewGuid(), "Kadın", "B", "B", "B");
        var rules = JsonSerializer.Serialize(new[] { rule, rule }, JsonOptions);
        CollectionAssert.AreEqual(new[] { "A", "B" }, ErpGroupMappingTargets.Read("rules", "A", rules, null));
        var pool = JsonSerializer.Serialize(new[] { new PoolTargetDto("B", "B", "B"), new PoolTargetDto("C", "C", "C") }, JsonOptions);
        CollectionAssert.AreEqual(new[] { "B", "C" }, ErpGroupMappingTargets.Read("pool", null, null, pool));
    }

    [TestMethod]
    public void Inbound_RejectsDifferentGroups_ButAllowsSameGroupThroughMultipleTargets()
    {
        var group = Guid.NewGuid();
        Assert.IsNull(ErpGroupMappingTargets.Resolve([]));
        Assert.AreEqual(group, ErpGroupMappingTargets.Resolve([group, group]));
        Assert.Throws<InvalidOperationException>(() => ErpGroupMappingTargets.Resolve([group, Guid.NewGuid()]));
    }

    [TestMethod]
    public void BrokenJson_IsNotSilentlyTreatedAsUnmapped()
    {
        Assert.Throws<JsonException>(() => ErpGroupMappingTargets.Read("rules", null, "invalid", null));
        Assert.Throws<InvalidOperationException>(() => ErpGroupMappingTargets.Read("unknown", null, null, null));
    }

    [TestMethod]
    public void IncompleteAndCondition_RejectsWholeRule()
    {
        var value = Guid.NewGuid();
        var rule = new MappingRuleDto(0, "cinsiyet", value, "Kadın", "A", "A", "A",
            [new("cinsiyet", value, "Kadın"), new("beden", Guid.Empty, "")]);
        var result = MappingRuleResolver.Normalize([rule]);
        Assert.IsNull(result.Rules);
        Assert.IsNotNull(result.Error);
    }

    [TestMethod]
    public void LegacyCondition_RemainsSupported()
    {
        var rule = new MappingRuleDto(0, "cinsiyet", Guid.NewGuid(), "Kadın", "A", "A", "A");
        var result = MappingRuleResolver.Normalize([rule]);
        Assert.IsNull(result.Error);
        Assert.AreEqual(1, result.Rules![0].EffectiveConditions().Count);
    }

    [TestMethod]
    public void PanelLookup_RequiresDictionaryAndUniqueTarget_NoNameFallback()
    {
        var group = Guid.NewGuid();
        var names = new Dictionary<string, List<string>> { ["kot ceket"] = ["19"] };
        var mappings = new Dictionary<string, List<Guid>> { ["19"] = [group] };
        Assert.AreEqual(group, ErpPanelGroupResolver.Resolve("Kot Ceket", names, mappings));
        Assert.IsNull(ErpPanelGroupResolver.Resolve("Ceket", names, mappings));
        Assert.IsNull(ErpPanelGroupResolver.Resolve("Kot Ceket", names, new Dictionary<string, List<Guid>>()));
        mappings["19"].Add(Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => ErpPanelGroupResolver.Resolve("Kot Ceket", names, mappings));
        names["kot ceket"].Add("20");
        Assert.Throws<InvalidOperationException>(() => ErpPanelGroupResolver.Resolve("Kot Ceket", names, mappings));
    }

    [TestMethod]
    public void PoolValidation_RejectsEmptySingleAndDuplicateCandidates()
    {
        Assert.IsNotNull(ErpGroupMappingTargets.ValidatePool(null));
        Assert.IsNotNull(ErpGroupMappingTargets.ValidatePool([new("A", "A", "A")]));
        Assert.IsNotNull(ErpGroupMappingTargets.ValidatePool([new("A", "A", "A"), new(" A ", "A", "A")]));
        Assert.IsNotNull(ErpGroupMappingTargets.ValidatePool([new("A", "A", "A"), new(" ", "", "")]));
        Assert.IsNull(ErpGroupMappingTargets.ValidatePool([new("A", "A", "A"), new("B", "B", "B")]));
    }

    [TestMethod]
    public void PanelMode_IsOptIn_AndRejectsInvalidTarget()
    {
        var options = new ErpSourceOptions();
        Assert.IsFalse(options.UsePanelGroupMappings);
        options.UsePanelGroupMappings = true;
        options.Validate();
        options.MappingTargetSystem = "trendyol";
        Assert.Throws<InvalidOperationException>(options.Validate);
    }
}
