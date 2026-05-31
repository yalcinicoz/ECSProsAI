using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ECSPros.Api.Authorization;

/// <summary>
/// Endpoint'e erişim için belirtilen permission'ın JWT claim'leri arasında bulunmasını zorunlu kılar.
/// super_admin rolü (claim: permission=*) tüm kontrolleri atlar.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : Attribute, IAuthorizationFilter
{
    private readonly string _permission;

    public RequirePermissionAttribute(string permission)
    {
        _permission = permission;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var claims = user.Claims;

        // Wildcard — super_admin / platform_admin tüm kontrolleri geçer
        if (claims.Any(c => c.Type == "permission" && c.Value == "*"))
            return;

        if (claims.Any(c => c.Type == "permission" && c.Value == _permission))
            return;

        context.Result = new ObjectResult(new { success = false, error = "Bu işlem için yetkiniz yok." })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}
