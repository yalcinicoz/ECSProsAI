namespace ECSPros.Api.Tests;

[TestClass]
public sealed class ProductImageUploadFailureTests
{
    [TestMethod]
    public void BasarisizHariciUpload_PendingMetadataBirakmiyor()
    {
        var controller = File.ReadAllText(RepoFile(
            "src", "ECSPros.Api", "Controllers", "ProductImageController.cs"));

        var upload = controller.IndexOf(
            "var uploadSuccess = await _imageUploadService.UploadAsync", StringComparison.Ordinal);
        var failureBranch = controller.IndexOf("if (!uploadSuccess)", upload, StringComparison.Ordinal);
        var cancelled = controller.IndexOf("ProductImageStatus.Cancelled", failureBranch, StringComparison.Ordinal);
        var deleted = controller.IndexOf("pendingImage.IsDeleted = true", cancelled, StringComparison.Ordinal);
        var persisted = controller.IndexOf(
            "await db.SaveChangesAsync(CancellationToken.None)", deleted, StringComparison.Ordinal);

        Assert.IsTrue(upload >= 0, "Harici upload sonucu kontrol edilmelidir.");
        Assert.IsTrue(failureBranch > upload, "Başarısız upload için ayrı dal bulunmalıdır.");
        Assert.IsTrue(cancelled > failureBranch, "Başarısız metadata Cancelled yapılmalıdır.");
        Assert.IsTrue(deleted > cancelled, "Başarısız metadata soft-delete edilmelidir.");
        Assert.IsTrue(persisted > deleted, "Client bağlantısından bağımsız kalıcılaştırılmalıdır.");
    }

    private static string RepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
            directory = directory.Parent;

        Assert.IsNotNull(directory, "Repository root bulunamadı.");
        return Path.Combine([directory.FullName, .. parts]);
    }
}
