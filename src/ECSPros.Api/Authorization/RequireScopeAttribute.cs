using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ECSPros.Api.Authorization;

/// <summary>
/// Partner API uçları için: istek YALNIZ bir API hesabı token'ıyla (type=api_client) ve belirtilen
/// scope claim'iyle geçer. İç uçların [Authorize]/RequirePermission'ının karşılığıdır; scope kataloğu
/// panel `Permissions`'tan bağımsızdır (ApiScopes). Üye/personel token'ı (scope claim'i yok) giremez.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequireScopeAttribute : Attribute, IAuthorizationFilter
{
    private readonly string _scope;

    public RequireScopeAttribute(string scope)
    {
        _scope = scope;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Yalnız makine kimliği — üye/personel token'ı partner yüzeyine giremez.
        if (user.FindFirst("type")?.Value != "api_client")
        {
            context.Result = Forbidden();
            return;
        }

        if (!user.Claims.Any(c => c.Type == "scope" && c.Value == _scope))
            context.Result = Forbidden();
    }

    private static ObjectResult Forbidden() =>
        new(new { success = false, error = "Bu işlem için gerekli API yetkisi (scope) yok." })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
}
