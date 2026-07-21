using ECSPros.Iam.Domain.Entities;

namespace ECSPros.Iam.Application.Services;

public interface IJwtTokenService
{
    string GenerateAccessToken(User user, IEnumerable<string> permissions);
    string GenerateRefreshToken();
    Guid? ValidateRefreshToken(string tokenHash);

    /// <summary>API hesabı (makine kimliği) için client_credentials token'ı — type=api_client,
    /// scope claim'leri (tipten çözülmüş) taşır. 15 dk ömür, refresh yok.</summary>
    string GenerateApiClientToken(ApiClient client, IEnumerable<string> scopes);
}
