using ECSPros.Api.Services.Storage;
using ImageMagick;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace ECSPros.Api.Tests;

[TestClass]
public class DualTargetCatalogImageUploadServiceTests
{
    [TestMethod]
    public void UygulamaVarsayilani_CiftHedefAdapteriniAktifTutar()
    {
        var settingsJson = File.ReadAllText(RepoFile("src", "ECSPros.Api", "appsettings.json"));
        using var settings = JsonDocument.Parse(settingsJson);
        Assert.IsTrue(settings.RootElement
            .GetProperty("CatalogImageStorage")
            .GetProperty("Enabled")
            .GetBoolean());

        var program = File.ReadAllText(RepoFile("src", "ECSPros.Api", "Program.cs"));
        StringAssert.Contains(
            program,
            "GetValue(\"CatalogImageStorage:Enabled\", true)",
            "Ayar anahtarı deployment config'inde eksikse sessizce yerel diske dönülmemeli.");
    }

    [TestMethod]
    public async Task Upload_WebpVeJpegKopyalariniAyniBasenameIleYazar()
    {
        var sftp = new FakeSftpStore();
        var objects = new FakeObjectStore();
        var service = CreateService(sftp, objects);

        await using var source = SourcePng();
        var result = await service.UploadAsync(source, "P-1_set_sku_batch_01.webp");

        Assert.IsTrue(result);
        Assert.AreEqual("webp", service.GetStoredFileExtension("png"));
        Assert.AreEqual("P-1_set_sku_batch_01.webp", sftp.Uploads.Single().FileName);
        Assert.AreEqual("P-1_set_sku_batch_01.jpg", objects.Uploads.Single().FileName);
        CollectionAssert.AreEqual("RIFF"u8.ToArray(), sftp.Uploads.Single().Content[..4]);
        CollectionAssert.AreEqual(new byte[] { 0xff, 0xd8 }, objects.Uploads.Single().Content[..2]);
    }

    [TestMethod]
    public async Task Upload_TekHedefBasarisizsaBasariliKopyayiGeriAlir()
    {
        var sftp = new FakeSftpStore();
        var objects = new FakeObjectStore { FailUpload = true };
        var service = CreateService(sftp, objects);

        await using var source = SourcePng();
        var result = await service.UploadAsync(source, "product.webp");

        Assert.IsFalse(result);
        CollectionAssert.AreEqual(new[] { "product.webp" }, sftp.Deletes);
        Assert.AreEqual(0, objects.Deletes.Count);
    }

    [TestMethod]
    public async Task Delete_HerIkiHedefteDogruUzantiyiKullanir()
    {
        var sftp = new FakeSftpStore();
        var objects = new FakeObjectStore();
        var service = CreateService(sftp, objects);

        var result = await service.DeleteAsync("product.png");

        Assert.IsTrue(result);
        CollectionAssert.AreEqual(new[] { "product.webp" }, sftp.Deletes);
        CollectionAssert.AreEqual(new[] { "product.jpg" }, objects.Deletes);
    }

    private static DualTargetCatalogImageUploadService CreateService(
        ICatalogImageSftpStore sftp,
        ICatalogImageObjectStore objects)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CatalogImageStorage:ImageQuality"] = "80",
                ["CatalogImageStorage:PublicBaseUrl"] = "https://media.example.test/img/1200/85"
            })
            .Build();
        return new DualTargetCatalogImageUploadService(
            sftp, objects, new FakeSettingsProvider(), configuration,
            NullLogger<DualTargetCatalogImageUploadService>.Instance);
    }

    private static MemoryStream SourcePng()
    {
        using var image = new MagickImage(MagickColors.Red, 2, 2);
        image.Format = MagickFormat.Png;
        return new MemoryStream(image.ToByteArray());
    }

    private static string RepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. parts]);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Repository dosyası bulunamadı: {Path.Combine(parts)}");
    }

    private sealed class FakeSftpStore : ICatalogImageSftpStore
    {
        public List<(string FileName, byte[] Content)> Uploads { get; } = [];
        public List<string> Deletes { get; } = [];

        public Task UploadAsync(
            CatalogImageStorageSettings settings, string fileName, byte[] content, CancellationToken ct)
        {
            Uploads.Add((fileName, content));
            return Task.CompletedTask;
        }

        public Task DeleteAsync(CatalogImageStorageSettings settings, string fileName, CancellationToken ct)
        {
            Deletes.Add(fileName);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeObjectStore : ICatalogImageObjectStore
    {
        public bool FailUpload { get; init; }
        public List<(string FileName, byte[] Content)> Uploads { get; } = [];
        public List<string> Deletes { get; } = [];

        public Task UploadAsync(
            CatalogImageStorageSettings settings, string fileName, byte[] content, CancellationToken ct)
        {
            if (FailUpload) throw new InvalidOperationException("simulated object storage failure");
            Uploads.Add((fileName, content));
            return Task.CompletedTask;
        }

        public Task DeleteAsync(CatalogImageStorageSettings settings, string fileName, CancellationToken ct)
        {
            Deletes.Add(fileName);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSettingsProvider : ICatalogImageStorageSettingsProvider
    {
        public Task<CatalogImageStorageSettings> GetAsync(CancellationToken ct) => Task.FromResult(new CatalogImageStorageSettings(
            ImageQuality: 80,
            SftpHost: "sftp.example.test",
            SftpPort: 22,
            SftpUsername: "user",
            SftpPassword: "password",
            SftpBasePath: "/images",
            S3ServiceUrl: "https://s3.example.test/",
            S3Bucket: "images",
            S3AccessKey: "access",
            S3SecretKey: "secret",
            S3ForcePathStyle: true,
            Timeout: TimeSpan.FromSeconds(30),
            MaxErrorRetry: 3));
    }
}
