using ECSPros.Iam.Application.Yetkilendirme;

namespace ECSPros.Api.Authorization;

/// <summary>Y5: denetim kaydına yazılacak aktör ve ağ bilgisi (istek bağlamından).</summary>
public sealed class YetkiDenetimBaglami(IHttpContextAccessor http) : IYetkiDenetimBaglami
{
    public Guid? AktorId =>
        Guid.TryParse(http.HttpContext?.User?.FindFirst("sub")?.Value, out var id) ? id : null;

    public string? Ip => http.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent
    {
        get
        {
            var ua = http.HttpContext?.Request.Headers.UserAgent.ToString();
            return string.IsNullOrWhiteSpace(ua) ? null : (ua.Length > 400 ? ua[..400] : ua);
        }
    }
}
