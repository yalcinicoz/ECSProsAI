using ECSPros.Crm.Domain.Entities;

namespace ECSPros.Crm.Application.Services;

public interface IMemberTokenService
{
    string GenerateAccessToken(Member member);
    string GenerateRefreshToken();
    string HashRefreshToken(string refreshToken);
}
