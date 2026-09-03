using ECSPros.Api.Services.Storage;
using ECSPros.Api.Services.Store;
using ImageMagick;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace ECSPros.Api.Tests;

[TestClass]
public class StorefrontMediaUploadServiceTests
{
    [TestMethod]
    public async Task Upload_CdnEtkinseUrunKokundenAyriDesktopAgacinaCiftYazar()
    {
        var sftp = new FakeSftpStore();
        var objects = new FakeObjectStore();
        var service = CreateService(sftp, objects);

        await using var source = SourcePng();
        var result = await service.UploadAsync("desktop", "hero.png", source, "image/png");

        Assert.AreEqual(5, sftp.Uploads.Count);
        Assert.AreEqual(5, objects.Uploads.Count);
        Assert.IsTrue(sftp.Uploads.All(x => x.BasePath == "/var/www/html/storefront"));
        Assert.IsTrue(sftp.Uploads.All(x => x.Key.StartsWith("pages/desktop/")));
        Assert.IsTrue(objects.Uploads.All(x => x.Key.StartsWith("storefront/pages/desktop/")));
        StringAssert.StartsWith(result.PublicUrl, "https://media.example.test/storefront/pages/desktop/");
        StringAssert.EndsWith(result.PublicUrl, "/hero.png");
        CollectionAssert.AreEquivalent(
            new[] { "hero_w480.webp", "hero_w800.webp", "hero_w1200.webp", "hero_w1920.webp" },
            sftp.Uploads.Skip(1).Select(x => Path.GetFileName(x.Key)).ToArray());
    }

    [TestMethod]
    public async Task Upload_MenuGorseliniSayfaGorsellerindenAyriMenuAgacinaCiftYazar()
    {
        var sftp = new FakeSftpStore();
        var objects = new FakeObjectStore();
        var service = CreateService(sftp, objects);

        await using var source = SourcePng();
        var result = await service.UploadAsync("menu", "category.png", source, "image/png");

        Assert.AreEqual(5, sftp.Uploads.Count);
        Assert.IsTrue(sftp.Uploads.All(x => x.Key.StartsWith("menus/")));
        Assert.IsTrue(objects.Uploads.All(x => x.Key.StartsWith("storefront/menus/")));
        StringAssert.StartsWith(result.PublicUrl, "https://media.example.test/storefront/menus/");
        StringAssert.EndsWith(result.PublicUrl, "/category.png");
    }

    [TestMethod]
    public async Task Upload_ObjectHedefiBasarisizsaSftpKopyasiniGeriAlir()
    {
        var sftp = new FakeSftpStore();
        var objects = new FakeObjectStore { FailUpload = true };
        var service = CreateService(sftp, objects);

        await using var source = SourcePng();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            service.UploadAsync("mobile", "hero.png", source, "image/png"));

        Assert.AreEqual(1, sftp.Deletes.Count);
        Assert.AreEqual(0, objects.Deletes.Count);
        StringAssert.Contains(sftp.Deletes.Single(), "pages/mobile/");
    }

    [TestMethod]
    public async Task Upload_StorefrontKokuUrunImagesKokununAltindaysaReddeder()
    {
        var service = CreateService(
            new FakeSftpStore(), new FakeObjectStore(), "/var/www/html/images/storefront");
        await using var source = SourcePng();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            service.UploadAsync("desktop", "hero.png", source, "image/png"));
    }

    [TestMethod]
    public void Srcset_CdnVitrinUrlindeYuklenenWebpVaryantlariniKullanir()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["StorefrontMediaStorage:PublicBaseUrl"] = "https://media.example.test/storefront",
            ["StorefrontMediaStorage:ResponsiveVariantsEnabled"] = "true"
        });
        var provider = new VitrinSrcsetSaglayici(configuration, new MemoryCache(new MemoryCacheOptions()));

        var result = provider.Srcset(
            "https://media.example.test/storefront/pages/desktop/2026/09/hero.png");

        Assert.IsNotNull(result);
        StringAssert.Contains(result, "/hero_w480.webp 480w");
        StringAssert.Contains(result, "/hero_w1920.webp 1920w");
    }

    [TestMethod]
    public void Srcset_EskiCdnDosyalarindaVaryantKapisiAcikDegilseAdayBasmaz()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["StorefrontMediaStorage:PublicBaseUrl"] = "https://media.example.test/storefront"
        });
        var provider = new VitrinSrcsetSaglayici(configuration, new MemoryCache(new MemoryCacheOptions()));

        Assert.IsNull(provider.Srcset(
            "https://media.example.test/storefront/pages/desktop/2026/09/hero.png"));
    }

    private static StorefrontMediaUploadService CreateService(
        IStorefrontMediaSftpStore sftp,
        IStorefrontMediaObjectStore objects,
        string storefrontBasePath = "/var/www/html/storefront")
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["StorefrontMediaStorage:Enabled"] = "true",
            ["StorefrontMediaStorage:PublicBaseUrl"] = "https://media.example.test/storefront",
            ["StorefrontMediaStorage:SftpBasePath"] = storefrontBasePath,
            ["StorefrontMediaStorage:ObjectPrefix"] = "storefront",
            ["StorefrontMediaStorage:ImageQuality"] = "78"
        });
        return new StorefrontMediaUploadService(
            new FakeFileStorage(), sftp, objects, new FakeSettingsProvider(), configuration,
            NullLogger<StorefrontMediaUploadService>.Instance);
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static MemoryStream SourcePng()
    {
        using var image = new MagickImage(MagickColors.Red, 2000, 1000);
        image.Format = MagickFormat.Png;
        return new MemoryStream(image.ToByteArray());
    }

    private sealed class FakeSftpStore : IStorefrontMediaSftpStore
    {
        public List<(string BasePath, string Key)> Uploads { get; } = [];
        public List<string> Deletes { get; } = [];

        public Task UploadAsync(CatalogImageStorageSettings settings, string basePath,
            string relativeKey, byte[] content, CancellationToken ct)
        {
            Uploads.Add((basePath, relativeKey));
            return Task.CompletedTask;
        }

        public Task DeleteAsync(CatalogImageStorageSettings settings, string basePath,
            string relativeKey, CancellationToken ct)
        {
            Deletes.Add(relativeKey);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeObjectStore : IStorefrontMediaObjectStore
    {
        public bool FailUpload { get; init; }
        public List<(string Key, string ContentType)> Uploads { get; } = [];
        public List<string> Deletes { get; } = [];

        public Task UploadAsync(CatalogImageStorageSettings settings, string key,
            byte[] content, string contentType, CancellationToken ct)
        {
            if (FailUpload) throw new InvalidOperationException("simulated object failure");
            Uploads.Add((key, contentType));
            return Task.CompletedTask;
        }

        public Task DeleteAsync(CatalogImageStorageSettings settings, string key, CancellationToken ct)
        {
            Deletes.Add(key);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSettingsProvider : ICatalogImageStorageSettingsProvider
    {
        public Task<CatalogImageStorageSettings> GetAsync(CancellationToken ct) =>
            Task.FromResult(new CatalogImageStorageSettings(
                80, "sftp.example.test", 22, "user", "password", "/var/www/html/images",
                "https://s3.example.test/", "bucket", "access", "secret", true,
                TimeSpan.FromSeconds(30), 3));
    }

    private sealed class FakeFileStorage : IFileStorage
    {
        public Task<StoredFile> SavePublicAsync(string category, string fileName, Stream content,
            string contentType, CancellationToken ct = default) =>
            throw new AssertFailedException("CDN etkin testte local fallback çağrılmamalı.");
        public Task<string> GetPrivateReadUrlAsync(string key, TimeSpan lifetime,
            CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeletePublicAsync(string key, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public string GetPublicUrl(string key) => throw new NotSupportedException();
    }
}
