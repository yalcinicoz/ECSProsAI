using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ECSPros.Iam.Application.Services;
using ECSPros.Iam.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ECSPros.Iam.Infrastructure.Services;

public class SupplierUserTokenService(IConfiguration configuration) : ISupplierUserTokenService
{
    private readonly string _secret = configuration["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret not configured.");
    private readonly string _issuer = configuration["Jwt:Issuer"] ?? "ECSPros";
    private readonly string _audience = configuration["Jwt:Audience"] ?? "ECSPros";

    public string GenerateAccessToken(SupplierUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("full_name", user.FullName),
            // Kimlik sınırı — DefaultPolicy (AdminOnly) bu type'ı iç uçlardan reddeder; yalnız SupplierOnly geçer.
            new("type", "supplier_user"),
            new("owner_type", "current_account"),
            new("owner_id", user.CurrentAccountId.ToString()), // panel owner-scope: kendi cari kartı
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    public string HashRefreshToken(string refreshToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
