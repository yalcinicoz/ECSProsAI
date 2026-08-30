using System.Text;
using ECSPros.Api.Services.Storage;
using ECSPros.Catalog.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class StorageTests
{
    private string _testRoot = null!;

    [TestInitialize]
    public void Initialize()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "ecspros-storage-tests", Guid.NewGuid().ToString("N"));
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }

    [TestMethod]
    public async Task Local_SavePublicAsync_AtomikYazarVePublicUrlDoner()
    {
        var storage = new LocalFileStorage(Configuration(new Dictionary<string, string?>
        {
            ["Storage:Local:RootPath"] = _testRoot,
            ["Storage:PublicBaseUrl"] = "https://media.example.test/"
        }));
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("media-content"));

        var result = await storage.SavePublicAsync(
            "reviews/2026", "photo.jpg", content, "image/jpeg");

        Assert.AreEqual("reviews/2026/photo.jpg", result.Key);
        Assert.AreEqual("https://media.example.test/reviews/2026/photo.jpg", result.PublicUrl);
        Assert.IsNotNull(result.PhysicalPath);
        Assert.AreEqual("media-content", await File.ReadAllTextAsync(result.PhysicalPath));
        Assert.IsEmpty(Directory.GetFiles(_testRoot, "*.tmp", SearchOption.AllDirectories));
    }

    [TestMethod]
    public async Task Local_DeletePublicAsync_YalnizGuvenliKeyiSiler()
    {
        var storage = new LocalFileStorage(Configuration(new Dictionary<string, string?>
        {
            ["Storage:Local:RootPath"] = _testRoot,
            ["Storage:PublicBaseUrl"] = "/media"
        }));
        await using var content = new MemoryStream([1, 2, 3]);
        var saved = await storage.SavePublicAsync("requests", "file.bin", content, "application/octet-stream");

        await storage.DeletePublicAsync(saved.Key);

        Assert.IsFalse(File.Exists(saved.PhysicalPath));
        await Assert.ThrowsAsync<ArgumentException>(() => storage.DeletePublicAsync("../outside.bin"));
    }

    [TestMethod]
    public async Task CatalogAdapter_ImageVeVideoIcinAyrikKeyKullanir()
    {
        var storage = new LocalFileStorage(Configuration(new Dictionary<string, string?>
        {
            ["Storage:Local:RootPath"] = _testRoot,
            ["Storage:PublicBaseUrl"] = "https://media.example.test"
        }));
        var adapter = new CatalogStorageUploadService(
            storage, NullLogger<CatalogStorageUploadService>.Instance);
        var imageService = (IImageUploadService)adapter;
        var videoService = (IVideoUploadService)adapter;
        await using var image = new MemoryStream([1]);
        await using var video = new MemoryStream([2]);

        Assert.IsTrue(await imageService.UploadAsync(image, "product.jpg"));
        Assert.IsTrue(await videoService.UploadAsync(video, "product.mp4"));
        Assert.AreEqual(
            "https://media.example.test/catalog/images/products/product.jpg",
            imageService.GetPublicUrl("product.jpg"));
        Assert.AreEqual(
            "https://media.example.test/catalog/videos/products/product.mp4",
            videoService.GetPublicUrl("product.mp4"));
        Assert.IsTrue(await imageService.DeleteAsync("product.jpg"));
        Assert.IsTrue(await videoService.DeleteAsync("product.mp4"));
    }

    [TestMethod]
    [DataRow("../outside", "photo.jpg")]
    [DataRow("reviews", "../photo.jpg")]
    [DataRow("reviews", "folder/photo.jpg")]
    public async Task Local_SavePublicAsync_PathTraversalReddeder(string category, string fileName)
    {
        var storage = new LocalFileStorage(Configuration(new Dictionary<string, string?>
        {
            ["Storage:Local:RootPath"] = _testRoot
        }));
        await using var content = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            storage.SavePublicAsync(category, fileName, content, "image/jpeg"));
    }

    [TestMethod]
    public void S3_EksikZorunluConfigIleBaslamaz()
    {
        var configuration = Configuration(new Dictionary<string, string?>());

        Assert.ThrowsExactly<InvalidOperationException>(() => new S3FileStorage(configuration));
    }

    [TestMethod]
    public void S3_HttpEndpointAcikcaIzinVerilmediyseReddeder()
    {
        var configuration = S3Configuration(allowHttp: false);

        Assert.ThrowsExactly<InvalidOperationException>(() => new S3FileStorage(configuration));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(10081)]
    public async Task S3_GecersizSignedUrlOmrunuReddeder(int minutes)
    {
        using var storage = new S3FileStorage(S3Configuration(allowHttp: true));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            storage.GetPrivateReadUrlAsync("private/file.pdf", TimeSpan.FromMinutes(minutes)));
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static IConfiguration S3Configuration(bool allowHttp) =>
        Configuration(new Dictionary<string, string?>
        {
            ["Storage:Provider"] = "S3",
            ["Storage:PublicBaseUrl"] = "https://media.example.test",
            ["Storage:S3:ServiceUrl"] = "http://127.0.0.1:9000",
            ["Storage:S3:Bucket"] = "ecspros-test",
            ["Storage:S3:AccessKey"] = "test-access-key",
            ["Storage:S3:SecretKey"] = "test-secret-key",
            ["Storage:S3:AllowHttp"] = allowHttp.ToString()
        });
}
