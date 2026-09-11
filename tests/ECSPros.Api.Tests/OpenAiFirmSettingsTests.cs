using System.Text.Json;
using ECSPros.Api.Services.AiReporting;
using ECSPros.Core.Domain.Entities;
using ECSPros.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class OpenAiFirmSettingsTests
{
    [TestMethod]
    public void ExplicitSharedAccountDoesNotRequireEmploymentFirm()
    {
        Assert.AreEqual(FirmId, OpenAiFirmSettingsProvider.ResolveAccount(FirmId, null, false, null));
        Assert.AreEqual(FirmId, OpenAiFirmSettingsProvider.ResolveAccount(FirmId, Guid.NewGuid(), false, FirmId));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
            OpenAiFirmSettingsProvider.ResolveAccount(FirmId, null, true, Guid.NewGuid()));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
            OpenAiFirmSettingsProvider.ResolveAccount(null, null, false, null));
    }

    [TestMethod]
    public void InvalidSharedAccountFailsClosed()
    {
        Assert.IsNull(OpenAiFirmSettingsProvider.ParseSharedAccount(null));
        Assert.AreEqual(FirmId, OpenAiFirmSettingsProvider.ParseSharedAccount(FirmId.ToString()));
        Assert.ThrowsExactly<InvalidOperationException>(() => OpenAiFirmSettingsProvider.ParseSharedAccount("invalid"));
        Assert.ThrowsExactly<InvalidOperationException>(() => OpenAiFirmSettingsProvider.ParseSharedAccount(Guid.Empty.ToString()));
    }
    [TestMethod]
    public void FirmOptionsOnlyExposeActiveAuthorizedFirms()
    {
        var own = new Firm { Id = FirmId, Code = "own" };
        var other = new Firm { Code = "other" };
        var inactive = new Firm { IsActive = false };
        var deleted = new Firm { IsDeleted = true };
        var source = new[] { own, other, inactive, deleted }.AsQueryable();
        Assert.AreEqual(FirmId, OpenAiFirmSettingsProvider.FirmOptions(source, FirmId, false).Single().Id);
        Assert.AreEqual(0, OpenAiFirmSettingsProvider.FirmOptions(source, null, false).Count());
        Assert.AreEqual(2, OpenAiFirmSettingsProvider.FirmOptions(source, null, true).Count());
    }

    [TestMethod]
    public void FirmOptionsSqlDoesNotLoadSensitiveFirmDataOrIntegrations()
    {
        using var db = new CoreDbContext(new DbContextOptionsBuilder<CoreDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=translation_only;Username=test").Options);
        var sql = OpenAiFirmSettingsProvider.FirmOptions(db.Firms, FirmId, false).ToQueryString();
        StringAssert.Contains(sql, "\"NameI18n\"");
        foreach (var field in new[] { "Credentials", "TaxNumber", "Address", "Phone", "Email", "core_firm_platform_integrations" })
            Assert.IsFalse(sql.Contains(field), field);
    }

    [TestMethod]
    public void EligibleQueryTranslatesToPostgres_WithoutLoadingCredentials()
    {
        using var db = new CoreDbContext(new DbContextOptionsBuilder<CoreDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=translation_only;Username=test")
            .Options);
        var sql = OpenAiFirmSettingsProvider.Eligible(db.FirmPlatformIntegrations, FirmId, DateTime.UtcNow)
            .Select(x => x.Id).Take(2).ToQueryString();
        StringAssert.Contains(sql, "\"FirmId\"");
        StringAssert.Contains(sql, "\"FirmPlatformId\" IS NULL");
        StringAssert.Contains(sql, "openai_reporting");
        Assert.IsFalse(sql.Contains("\"Credentials\""));
    }
    private static readonly Guid FirmId = Guid.NewGuid();
    private static FirmPlatformIntegration Integration() => new()
    {
        FirmId = FirmId, Status = "active", Firm = new() { IsActive = true },
        IntegrationService = new() { Code = "openai_reporting", ServiceType = "ai_reporting", IsAvailable = true },
        Credentials = new() { ["apiKey"] = "fake-key-not-real" }, Settings = new() { ["model"] = "configured-model" }
    };

    [TestMethod]
    public void FirmCannotBeChangedByOrdinaryUser_AndNoImplicitFirstFirm()
    {
        Assert.AreEqual(FirmId, OpenAiFirmSettingsProvider.ResolveFirm(FirmId, false, null));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => OpenAiFirmSettingsProvider.ResolveFirm(FirmId, false, Guid.NewGuid()));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => OpenAiFirmSettingsProvider.ResolveFirm(null, false, FirmId));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => OpenAiFirmSettingsProvider.ResolveFirm(null, true, null));
        Assert.AreEqual(FirmId, OpenAiFirmSettingsProvider.ResolveFirm(null, true, FirmId));
    }

    [TestMethod]
    public void ActiveWindowAndFirmScopeAreMandatory()
    {
        var now = DateTime.UtcNow;
        var valid = Integration();
        var other = Integration(); other.FirmId = Guid.NewGuid();
        var expired = Integration(); expired.EndDate = now;
        var future = Integration(); future.StartDate = now.AddMinutes(1);
        var draft = Integration(); draft.Status = "draft";
        var platform = Integration(); platform.FirmPlatformId = Guid.NewGuid();
        var deleted = Integration(); deleted.IsDeleted = true;
        var unavailable = Integration(); unavailable.IntegrationService.IsAvailable = false;
        var inactiveFirm = Integration(); inactiveFirm.Firm.IsActive = false;
        var result = OpenAiFirmSettingsProvider.Eligible(new[]
            { valid, other, expired, future, draft, platform, deleted, unavailable, inactiveFirm }.AsQueryable(), FirmId, now).ToArray();
        Assert.AreEqual(1, result.Length);
        Assert.AreSame(valid, result[0]);
    }

    [TestMethod]
    public void MissingOrAmbiguousIntegrationNeverPicksFirst()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => OpenAiFirmSettingsProvider.ChooseUnique(Array.Empty<Guid>()));
        Assert.ThrowsExactly<InvalidOperationException>(() => OpenAiFirmSettingsProvider.ChooseUnique(new[] { Guid.NewGuid(), Guid.NewGuid() }));
    }

    [TestMethod]
    public void SecretsAreNotInJsonOrToString_AndSettingsCannotOverrideKey()
    {
        var integration = Integration();
        integration.Settings["apiKey"] = "wrong-settings-key";
        var settings = OpenAiFirmSettings.From(integration);
        Assert.AreEqual("fake-key-not-real", settings.ApiKey);
        Assert.IsFalse(JsonSerializer.Serialize(settings).Contains("fake-key"));
        Assert.IsFalse(settings.ToString().Contains("fake-key"));
        integration.Credentials.Clear();
        Assert.ThrowsExactly<InvalidOperationException>(() => OpenAiFirmSettings.From(integration));
    }

    [TestMethod]
    public void JsonStringCredentialsWork_MaskedOrWrongTypeDoNot()
    {
        var integration = Integration();
        integration.Credentials["apiKey"] = JsonSerializer.SerializeToElement("test-key");
        Assert.AreEqual("test-key", OpenAiFirmSettings.From(integration).ApiKey);
        integration.Credentials["apiKey"] = "•••";
        Assert.ThrowsExactly<InvalidOperationException>(() => OpenAiFirmSettings.From(integration));
        integration.Credentials["apiKey"] = 123;
        Assert.ThrowsExactly<InvalidOperationException>(() => OpenAiFirmSettings.From(integration));
    }
}
