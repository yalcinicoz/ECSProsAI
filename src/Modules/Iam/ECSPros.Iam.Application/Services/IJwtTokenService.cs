using ECSPros.Iam.Domain.Entities;

namespace ECSPros.Iam.Application.Services;

public interface IJwtTokenService
{
    /// <summary>Access token — Y1 (K3): yetki listesi TAŞIMAZ; yalnız kimlik + süper admin bayrağı.
    /// Yetki her istekte IEtkinYetkiServisi'nden okunur.</summary>
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    Guid? ValidateRefreshToken(string tokenHash);

    /// <summary>API hesabı (makine kimliği) için client_credentials token'ı — type=api_client,
    /// scope claim'leri (tipten çözülmüş) taşır. 15 dk ömür, refresh yok.</summary>
    string GenerateApiClientToken(ApiClient client, IEnumerable<string> scopes);
}
