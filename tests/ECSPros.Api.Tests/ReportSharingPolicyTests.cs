using ECSPros.Api.Services.AiReporting;
using ECSPros.Shared.Kernel.Authorization;

namespace ECSPros.Api.Tests;

[TestClass]
public class ReportSharingPolicyTests
{
    [TestMethod]
    public void SharingParticipantsMustBothBeActive()
    {
        Assert.IsTrue(ReportSharingPolicy.ActiveParticipants(true, true));
        Assert.IsFalse(ReportSharingPolicy.ActiveParticipants(false, true));
        Assert.IsFalse(ReportSharingPolicy.ActiveParticipants(true, false));
        Assert.IsFalse(ReportSharingPolicy.ActiveParticipants(false, false));
    }
    [TestMethod]
    public void SharingNeedsDedicatedUnscopedPermissionAndReportPermission()
    {
        var rights = new Dictionary<string, HashSet<Guid>?> { [ReportDictionary.UsePermission] = null, ["inventory.view"] = null };
        Assert.IsFalse(ReportSharingPolicy.CanShare(new(false, rights)));
        rights[ReportDictionary.SharePermission] = null;
        Assert.IsTrue(ReportSharingPolicy.CanShare(new(false, rights)));
        rights[ReportDictionary.SharePermission] = new() { Guid.NewGuid() };
        Assert.IsFalse(ReportSharingPolicy.CanShare(new(false, rights)));
        rights[ReportDictionary.SharePermission] = null;
        rights.Remove(ReportDictionary.UsePermission);
        Assert.IsFalse(ReportSharingPolicy.CanShare(new(false, rights)));
    }
    [TestMethod]
    public void TokensAreIndependentAndRevocationOrWrongTokenFails()
    {
        var token = ReportSharingPolicy.NewToken();
        Assert.AreEqual(64, token.Length);
        Assert.IsTrue(ReportSharingPolicy.TokenMatches(token, token));
        Assert.IsFalse(ReportSharingPolicy.TokenMatches(token, ReportSharingPolicy.NewToken()));
        Assert.IsFalse(ReportSharingPolicy.TokenMatches(null, token));
        Assert.IsFalse(ReportSharingPolicy.TokenMatches(token, null));
        Assert.IsFalse(ReportSharingPolicy.TokenMatches(token, "short"));
    }
}
