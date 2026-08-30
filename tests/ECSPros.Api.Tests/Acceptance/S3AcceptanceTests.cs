using System.Text;
using ECSPros.Api.Services.Storage;
using Microsoft.Extensions.Configuration;

namespace ECSPros.Api.Tests.Acceptance;

[TestClass]
[TestCategory("Acceptance")]
[TestCategory("S3")]
[DoNotParallelize]
public sealed class S3AcceptanceTests
{
    [TestMethod]
    public async Task IkiProvider_UploadSignedReadVeDeleteAkisiniPaylasir()
    {
        var values = RequireConfiguration();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        using var uploader = new S3FileStorage(configuration);
        using var reader = new S3FileStorage(configuration);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var fileName = $"{Guid.NewGuid():N}.txt";
        var key = $"acceptance/{fileName}";
        var expected = $"ecspros-s3-acceptance-{Guid.NewGuid():N}";

        try
        {
            await using var content = new MemoryStream(Encoding.UTF8.GetBytes(expected));
            var stored = await uploader.SavePublicAsync(
                "acceptance", fileName, content, "text/plain; charset=utf-8");
            Assert.AreEqual(key, stored.Key);

            var signedUrl = await reader.GetPrivateReadUrlAsync(key, TimeSpan.FromMinutes(5));
            var actual = await http.GetStringAsync(signedUrl);
            Assert.AreEqual(expected, actual, "İkinci provider yüklenen objeyi signed URL ile okuyamadı.");

            await reader.DeletePublicAsync(key);
            using var deletedResponse = await http.GetAsync(
                await uploader.GetPrivateReadUrlAsync(key, TimeSpan.FromMinutes(5)));
            Assert.IsFalse(deletedResponse.IsSuccessStatusCode, "Silinen acceptance objesi hâlâ okunabiliyor.");
        }
        finally
        {
            // Test yarıda kesilirse yalnız benzersiz kendi key'imizi temizlemeyi tekrar dene.
            try { await uploader.DeletePublicAsync(key); }
            catch { /* Asıl test hatasını gölgelememeli. */ }
        }
    }

    private static Dictionary<string, string?> RequireConfiguration()
    {
        const string prefix = "ECSPROS_ACCEPTANCE_S3_";
        string Required(string suffix)
            => AcceptanceTestEnvironment.Require(
                prefix + suffix, $"Acceptance:S3:{suffix}", prefix + suffix);

        var endpoint = Required("SERVICE_URL");
        var bucket = Required("BUCKET");
        if (!bucket.Contains("test", StringComparison.OrdinalIgnoreCase) &&
            !bucket.Contains("acceptance", StringComparison.OrdinalIgnoreCase))
            Assert.Fail("Güvenlik kapısı: S3 bucket adı 'test' veya 'acceptance' içermelidir.");
        if (!AcceptanceTestEnvironment.GetBoolean(
                prefix + "ALLOW_WRITE", "Acceptance:S3:ALLOW_WRITE"))
            Assert.Inconclusive($"S3 yazmalı test için {prefix}ALLOW_WRITE=true açıkça verilmelidir.");

        var isHttp = Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) &&
                     uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase);
        return new Dictionary<string, string?>
        {
            ["Storage:Provider"] = "S3",
            ["Storage:PublicBaseUrl"] = AcceptanceTestEnvironment.Optional(
                                            prefix + "PUBLIC_BASE_URL", "Acceptance:S3:PUBLIC_BASE_URL")
                                        ?? endpoint,
            ["Storage:S3:ServiceUrl"] = endpoint,
            ["Storage:S3:Bucket"] = bucket,
            ["Storage:S3:AccessKey"] = Required("ACCESS_KEY"),
            ["Storage:S3:SecretKey"] = Required("SECRET_KEY"),
            ["Storage:S3:Region"] = AcceptanceTestEnvironment.Optional(
                                          prefix + "REGION", "Acceptance:S3:REGION") ?? "us-east-1",
            ["Storage:S3:ForcePathStyle"] = AcceptanceTestEnvironment.Optional(
                                                  prefix + "FORCE_PATH_STYLE", "Acceptance:S3:FORCE_PATH_STYLE") ?? "true",
            ["Storage:S3:AllowHttp"] = isHttp.ToString()
        };
    }
}
