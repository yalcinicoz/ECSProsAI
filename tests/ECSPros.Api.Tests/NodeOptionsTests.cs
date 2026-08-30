using ECSPros.Api.Services;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class NodeOptionsTests
{
    [TestMethod]
    [DataRow("api", "Api", false)]
    [DataRow("WORKER", "Worker", true)]
    [DataRow(" Both ", "Both", true)]
    public void Dogrula_GecerliRoluCanonicalYapar(
        string role, string expectedRole, bool expectedWorkerRole)
    {
        var options = new NodeOptions { Id = " node-1 ", Role = role };

        options.Dogrula();

        Assert.AreEqual("node-1", options.Id);
        Assert.AreEqual(expectedRole, options.Role);
        Assert.AreEqual(expectedWorkerRole, options.WorkerRolu);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("invalid")]
    [DataRow("api-worker")]
    public void Dogrula_GecersizRoluReddeder(string role)
    {
        var options = new NodeOptions { Id = "node-1", Role = role };

        Assert.ThrowsExactly<InvalidOperationException>(options.Dogrula);
    }

    [TestMethod]
    public void Dogrula_BosNodeIdReddeder()
    {
        var options = new NodeOptions { Id = "  ", Role = "Api" };

        Assert.ThrowsExactly<InvalidOperationException>(options.Dogrula);
    }
}
