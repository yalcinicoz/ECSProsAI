using ECSPros.Catalog.Application.Services;
using Microsoft.AspNetCore.DataProtection;

namespace ECSPros.Api.Services.Storage;

public sealed class CatalogSettingSecretProtector : ICatalogSettingSecretProtector
{
    private const string ProtectedPrefix = "dp:v1:";
    private static readonly HashSet<string> SecretKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "ImageServer.FtpPassword",
        "VideoServer.FtpPassword",
        "ImageServer.SftpPassword",
        "ImageServer.S3AccessKey",
        "ImageServer.S3SecretKey"
    };

    private readonly IDataProtector _protector;

    public CatalogSettingSecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("ECSPros.Catalog.Settings.Secrets.v1");
    }

    public bool IsSecret(string key) => SecretKeys.Contains(key);

    public string Protect(string value) => string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : ProtectedPrefix + _protector.Protect(value);

    public string Unprotect(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(ProtectedPrefix, StringComparison.Ordinal))
            return value;

        return _protector.Unprotect(value[ProtectedPrefix.Length..]);
    }
}
