using ECSPros.Api.Services;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class PostgresOptionsTests
{
    [TestMethod]
    public void Validate_GecerliSinirlariKabulEder()
    {
        var options = new PostgresOptions
        {
            HostRecheckSeconds = 10,
            MinPoolSize = 5,
            MaxPoolSize = 200,
            TimeoutSeconds = 5,
            CommandTimeoutSeconds = 30
        };

        options.Validate();
    }

    [TestMethod]
    [DataRow(-1, 100)]
    [DataRow(101, 100)]
    [DataRow(0, 0)]
    [DataRow(0, 1001)]
    public void Validate_GecersizPoolSinirlariniReddeder(int minPoolSize, int maxPoolSize)
    {
        var options = new PostgresOptions
        {
            MinPoolSize = minPoolSize,
            MaxPoolSize = maxPoolSize
        };

        Assert.ThrowsExactly<InvalidOperationException>(options.Validate);
    }

    [TestMethod]
    [DataRow(0, 5, 30)]
    [DataRow(301, 5, 30)]
    [DataRow(10, 0, 30)]
    [DataRow(10, 61, 30)]
    [DataRow(10, 5, 0)]
    [DataRow(10, 5, 601)]
    public void Validate_GecersizTimeoutSinirlariniReddeder(
        int hostRecheck, int timeout, int commandTimeout)
    {
        var options = new PostgresOptions
        {
            HostRecheckSeconds = hostRecheck,
            TimeoutSeconds = timeout,
            CommandTimeoutSeconds = commandTimeout
        };

        Assert.ThrowsExactly<InvalidOperationException>(options.Validate);
    }
}
