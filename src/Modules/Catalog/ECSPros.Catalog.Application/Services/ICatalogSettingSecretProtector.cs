namespace ECSPros.Catalog.Application.Services;

/// <summary>
/// Catalog setting içindeki hassas değerleri istemciye maskeleyip kalıcı depoda korur.
/// Uygulama katmanı şifreleme teknolojisini bilmez; host gerçek implementasyonu sağlar.
/// </summary>
public interface ICatalogSettingSecretProtector
{
    const string MaskedValue = "•••";

    bool IsSecret(string key);
    string Protect(string value);
    string Unprotect(string value);
}
