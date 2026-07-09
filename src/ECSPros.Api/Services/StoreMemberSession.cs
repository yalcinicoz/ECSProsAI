using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ECSPros.Api.Services;

/// <summary>Razor sayfa render'ında bilinen üye kimliği (D1) — HttpOnly cookie'deki JWT'den.</summary>
public record StoreUyeKimlik(Guid MemberId, string FullName, string? Email);

public interface IStoreMemberSession
{
    /// <summary>Cookie'deki access token geçerliyse üye kimliğini, değilse null döner.</summary>
    Task<StoreUyeKimlik?> MevcutUyeAsync(HttpContext context);
}

/// <summary>
/// D1: Store JWT'si login/refresh yanıtlarında HttpOnly "ecspros_member" cookie'sine de
/// yazılır; SSR sayfaları bu servisle kimliği çözer (StorePageController → ViewData["MsUye"]).
/// JS tarafı localStorage token'larıyla çalışmaya devam eder — cookie yalnız sayfa render'ı
/// içindir, API çağrılarında Authorization header esastır.
/// NOT: JwtSecurityTokenHandler DEĞİL JsonWebTokenHandler kullanılır — JwtBearer 8.0.14'ün
/// getirdiği IdentityModel 7.1.2'de eski handler geçerli exp'li token'a bile
/// SecurityTokenNoExpirationException fırlatıyor (pipeline da JsonWebTokenHandler kullanır).
/// </summary>
public class StoreMemberSession : IStoreMemberSession
{
    public const string CookieAdi = "ecspros_member";

    private readonly TokenValidationParameters _dogrulama;
    private static readonly JsonWebTokenHandler Handler = new();

    public StoreMemberSession(IConfiguration configuration)
    {
        _dogrulama = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = configuration["Jwt:Issuer"],
            ValidAudience = configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(configuration["Jwt:Secret"]
                    ?? throw new InvalidOperationException("Jwt:Secret is not configured."))),
            ClockSkew = TimeSpan.Zero
        };
    }

    public async Task<StoreUyeKimlik?> MevcutUyeAsync(HttpContext context)
    {
        var token = context.Request.Cookies[CookieAdi];
        if (string.IsNullOrEmpty(token)) return null;

        try
        {
            var sonuc = await Handler.ValidateTokenAsync(token, _dogrulama);
            if (!sonuc.IsValid) return null; // süresi dolmuş/bozuk = misafir render (JS refresh tazeler)

            var kimlik = sonuc.ClaimsIdentity;
            if (kimlik.FindFirst("type")?.Value != "member") return null;

            var sub = kimlik.FindFirst("sub")?.Value;
            if (!Guid.TryParse(sub, out var memberId)) return null;

            return new StoreUyeKimlik(
                memberId,
                kimlik.FindFirst("full_name")?.Value ?? "",
                kimlik.FindFirst("email")?.Value);
        }
        catch
        {
            return null;
        }
    }
}
