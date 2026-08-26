namespace ECSPros.Shared.Contracts;

/// <summary>
/// Sosyal giriş (OAuth) sağlayıcı ayarlarının kaynağı — firma/platform bazlı
/// FirmPlatformIntegration (ServiceType=social_login) kayıtlarından çözülür
/// (SMTP deseni). Kayıt yoksa veya eksikse null döner → vitrin ilgili butonu
/// gizler, OAuth akışı başlatmaz.
/// </summary>
public interface ISocialLoginSettingsProvider
{
    /// <summary>provider = "google" | "facebook". Platforma özel kayıt firma-geneline tercih edilir.</summary>
    Task<SocialLoginSettings?> GetAsync(string provider, Guid firmPlatformId, CancellationToken ct = default);
}

public record SocialLoginSettings(
    string Provider,        // "google" | "facebook"
    string ClientId,
    string ClientSecret,
    string? RedirectUri,    // null → platform host'undan üretilir
    IReadOnlyList<string>? Scopes = null,   // null/boş → sağlayıcı varsayılanı
    string? GraphApiVersion = null);        // yalnız Facebook; boş → v26.0
