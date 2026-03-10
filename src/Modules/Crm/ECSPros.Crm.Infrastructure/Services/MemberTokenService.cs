using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ECSPros.Crm.Infrastructure.Services;

public class MemberTokenService(IConfiguration configuration) : IMemberTokenService
{
    private readonly string _secret = configuration["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret not configured.");
    private readonly string _issuer = configuration["Jwt:Issuer"] ?? "ECSPros";
    private readonly string _audience = configuration["Jwt:Audience"] ?? "ECSPros";

    public string GenerateAccessToken(Member member)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, member.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, member.Email ?? string.Empty),
            new("full_name", $"{member.FirstName} {member.LastName}"),
            new("type", "member"),
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
