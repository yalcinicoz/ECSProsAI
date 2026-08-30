using ECSPros.Api.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class ReverseProxyOptionsTests
{
    [TestMethod]
    public void Create_YalnizAcikcaTanimliProxyVeAglariGuvenir()
    {
        var configured = new ReverseProxyOptions
        {
            KnownProxies = ["10.20.0.10"],
            KnownNetworks = ["10.30.0.0/24", "2001:db8::/64"],
            ForwardLimit = 2
        };

        var options = configured.CreateForwardedHeadersOptions();

        Assert.AreEqual(2, options.ForwardLimit);
        CollectionAssert.AreEqual(new[] { IPAddress.Parse("10.20.0.10") }, options.KnownProxies.ToArray());
        Assert.AreEqual(2, options.KnownNetworks.Count);
        Assert.IsFalse(options.KnownProxies.Contains(IPAddress.Parse("10.20.0.11")));
        Assert.AreEqual(
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
            options.ForwardedHeaders);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(6)]
    public void Create_GecersizForwardLimitReddeder(int forwardLimit)
    {
        var options = new ReverseProxyOptions { ForwardLimit = forwardLimit };

        Assert.ThrowsExactly<InvalidOperationException>(options.CreateForwardedHeadersOptions);
    }

    [TestMethod]
    [DataRow("not-an-ip")]
    [DataRow("10.0.0.999")]
    public void Create_GecersizProxyAdresiReddeder(string proxy)
    {
        var options = new ReverseProxyOptions { KnownProxies = [proxy] };

        Assert.ThrowsExactly<InvalidOperationException>(options.CreateForwardedHeadersOptions);
    }

    [TestMethod]
    [DataRow("10.0.0.0")]
    [DataRow("10.0.0.0/33")]
    [DataRow("2001:db8::/129")]
    public void Create_GecersizCidrReddeder(string network)
    {
        var options = new ReverseProxyOptions { KnownNetworks = [network] };

        Assert.ThrowsExactly<InvalidOperationException>(options.CreateForwardedHeadersOptions);
    }

    [TestMethod]
    public async Task Middleware_GuvenilmeyenSokettenGelenXffVeCfBasliklariniYoksayar()
    {
        var options = new ReverseProxyOptions
        {
            KnownProxies = ["10.20.0.10"]
        }.CreateForwardedHeadersOptions();
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.25";
        context.Request.Headers["CF-Connecting-IP"] = "198.51.100.30";
        var middleware = CreateMiddleware(options);

        await middleware.Invoke(context);

        Assert.AreEqual(IPAddress.Parse("203.0.113.10"), context.Connection.RemoteIpAddress);
    }

    [TestMethod]
    public async Task Middleware_GuvenilirProxydenGelenXffAdresiniUygular()
    {
        var options = new ReverseProxyOptions
        {
            KnownProxies = ["10.20.0.10"]
        }.CreateForwardedHeadersOptions();
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.20.0.10");
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.25";
        var middleware = CreateMiddleware(options);

        await middleware.Invoke(context);

        Assert.AreEqual(IPAddress.Parse("198.51.100.25"), context.Connection.RemoteIpAddress);
    }

    private static ForwardedHeadersMiddleware CreateMiddleware(
        Microsoft.AspNetCore.Builder.ForwardedHeadersOptions options)
        => new(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options));
}
