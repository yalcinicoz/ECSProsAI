using ECSPros.Api.Services.Storage;
using Microsoft.AspNetCore.DataProtection;

namespace ECSPros.Api.Tests;

[TestClass]
public class CatalogSettingSecretProtectorTests
{
    [TestMethod]
    public void Protect_SecretDegeriSifrelerVeGeriCozer()
    {
        var protector = new CatalogSettingSecretProtector(new EphemeralDataProtectionProvider());

        var stored = protector.Protect("very-secret-value");

        Assert.AreNotEqual("very-secret-value", stored);
        StringAssert.StartsWith(stored, "dp:v1:");
        Assert.AreEqual("very-secret-value", protector.Unprotect(stored));
    }

    [TestMethod]
    public void Unprotect_EskiDuzDegeriGeriyeUyumluOkur()
    {
        var protector = new CatalogSettingSecretProtector(new EphemeralDataProtectionProvider());

        Assert.AreEqual("legacy-value", protector.Unprotect("legacy-value"));
        Assert.IsTrue(protector.IsSecret("ImageServer.S3SecretKey"));
        Assert.IsFalse(protector.IsSecret("ImageServer.S3Bucket"));
    }
}
