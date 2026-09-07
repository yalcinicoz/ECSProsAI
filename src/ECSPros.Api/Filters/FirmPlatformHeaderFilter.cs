using System.Reflection;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ECSPros.Api.Filters;

/// <summary>
/// A6 (2026-09-07, mobil): /api/store/* uçlarında kanal kimliği için TEK KURAL — istemci
/// <c>X-Firm-Platform: {guid}</c> başlığını gönderir; query (<c>?firmPlatformId=</c>) veya gövde
/// (<c>firmPlatformId</c>) alanı boş/eksikse bu filtre başlıktaki değeri yerine yazar. Eski
/// query/gövde kullanımı aynen çalışmaya devam eder (geriye uyumlu); ikisi de doluysa istemcinin
/// açık verdiği değer kazanır. Model bağlama SONRASI, aksiyon ÖNCESİ çalışır; yalnız
/// <c>Guid</c>/<c>Guid?</c> tipli <c>firmPlatformId</c> parametreleri ve gövde nesnelerinin
/// <c>FirmPlatformId</c> özelliği hedeflenir.
/// </summary>
public sealed class FirmPlatformHeaderFilter : IActionFilter
{
    public const string HeaderName = "X-Firm-Platform";
    private const string ParamName = "firmPlatformId";
    private const string PropName = "FirmPlatformId";

    public void OnActionExecuting(ActionExecutingContext context)
    {
        var path = context.HttpContext.Request.Path;
        if (!path.StartsWithSegments("/api/store", StringComparison.OrdinalIgnoreCase)) return;
        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var raw)) return;
        if (!Guid.TryParse(raw.ToString().Trim(), out var platformId) || platformId == Guid.Empty) return;

        // 1) Doğrudan parametre: [FromQuery] Guid firmPlatformId / Guid? firmPlatformId. Query'de değer yoksa
        //    model bağlayıcı anahtarı ActionArguments'a HİÇ koymaz (IsModelSet=false) — bu yüzden parametre
        //    tanımı üzerinden gidilir; boş/eksik olan başlıktan doldurulur.
        foreach (var prm in context.ActionDescriptor.Parameters)
        {
            if (!string.Equals(prm.Name, ParamName, StringComparison.OrdinalIgnoreCase)) continue;
            if (prm.ParameterType != typeof(Guid) && prm.ParameterType != typeof(Guid?)) continue;
            if (!context.ActionArguments.TryGetValue(prm.Name, out var mevcut) || mevcut is null
                || (mevcut is Guid g && g == Guid.Empty))
                context.ActionArguments[prm.Name] = platformId;
        }

        // 2) Gövde nesnesi: FirmPlatformId (Guid / Guid?) özelliği boşsa doldur
        foreach (var key in context.ActionArguments.Keys.ToList())
        {
            var value = context.ActionArguments[key];
            if (string.Equals(key, ParamName, StringComparison.OrdinalIgnoreCase)) continue;
            if (value is null) continue;
            var type = value.GetType();
            if (type.IsPrimitive || type == typeof(string) || type == typeof(Guid) || type.IsEnum) continue;
            var prop = type.GetProperty(PropName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop is null || !prop.CanWrite) continue;
            if (prop.PropertyType == typeof(Guid))
            {
                if ((Guid)prop.GetValue(value)! == Guid.Empty) prop.SetValue(value, platformId);
            }
            else if (prop.PropertyType == typeof(Guid?))
            {
                if (prop.GetValue(value) is null) prop.SetValue(value, platformId);
            }
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
