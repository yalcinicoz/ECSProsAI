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
        Assert.AreEqual("All", options.WorkerProfile);
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

    [TestMethod]
    [DataRow("Worker", "all", true, true, true, true, false, false)]
    [DataRow("Worker", " LEGACYIMPORT ", true, false, false, false, true, true)]
    [DataRow("Both", "LegacyImport", true, false, false, false, true, true)]
    [DataRow("Worker", "legacystock", false, false, true, false, false, true)]
    [DataRow("Worker", "erpsource", false, true, false, false, false, true)]
    [DataRow("Api", "LegacyImport", false, false, false, false, false, false)]
    public void Dogrula_WorkerProfiliniCanonicalYaparVeGruplariAyirir(
        string role, string workerProfile, bool legacyImport, bool erpSource, bool legacyStock, bool general,
        bool legacyOnly, bool isolated)
    {
        var options = new NodeOptions
        {
            Id = "node-1",
            Role = role,
            WorkerProfile = workerProfile
        };

        options.Dogrula();

        Assert.AreEqual(legacyImport, options.LegacyImportWorkerRolu);
        Assert.AreEqual(erpSource, options.ErpSourceWorkerRolu);
        Assert.AreEqual(legacyStock, options.LegacyStockWorkerRolu);
        Assert.AreEqual(general, options.GenelWorkerRolu);
        Assert.AreEqual(legacyOnly, options.SadeceLegacyImport);
        Assert.AreEqual(isolated, options.SadeceIzoleWorker);
    }

    [TestMethod]
    public void Dogrula_GecersizWorkerProfiliniReddeder()
    {
        var options = new NodeOptions
        {
            Id = "node-1",
            Role = "Worker",
            WorkerProfile = "Unknown"
        };

        Assert.ThrowsExactly<InvalidOperationException>(options.Dogrula);
    }
}
