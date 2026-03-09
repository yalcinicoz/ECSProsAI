using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ECSPros.Iam.Application.Services;
using ECSPros.Iam.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ECSPros.Iam.Infrastructure.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly string _secret;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expiryMinutes;

    public JwtTokenService(IConfiguration configuration)
    {
        _secret = configuration["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret is not configured.");
        _issuer = configuration["Jwt:Issuer"] ?? "ECSPros";
        _audience = configuration["Jwt:Audience"] ?? "ECSPros";
        _expiryMinutes = int.TryParse(configuration["Jwt:ExpiryMinutes"], out var m) ? m : 60;
    }

    public string GenerateAccessToken(User user, IEnumerable<string> permissions)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("username", user.Username),
            new("full_name", $"{user.FirstName} {user.LastName}"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (user.FirmId.HasValue)
            claims.Add(new Claim("firm_id", user.FirmId.Value.ToString()));

        foreach (var perm in permissions)
            claims.Add(new Claim("permission", perm));

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_expiryMinutes),
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

    public Guid? ValidateRefreshToken(string tokenHash) => null; // Session tablosundan kontrol edilecek
}
