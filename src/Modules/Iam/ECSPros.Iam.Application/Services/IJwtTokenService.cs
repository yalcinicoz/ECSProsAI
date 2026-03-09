using ECSPros.Iam.Domain.Entities;

namespace ECSPros.Iam.Application.Services;

public interface IJwtTokenService
{
    string GenerateAccessToken(User user, IEnumerable<string> permissions);
    string GenerateRefreshToken();
    Guid? ValidateRefreshToken(string tokenHash);
}
