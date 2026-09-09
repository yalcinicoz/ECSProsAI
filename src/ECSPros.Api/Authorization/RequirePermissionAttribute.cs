using ECSPros.Iam.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ECSPros.Api.Authorization;

/// <summary>
/// Uç bazlı yetki kontrolü.
///
/// Y1 (2026-09-09, karar K3 "anında etkili"): yetki artık TOKEN'DAN değil, her istekte
/// <see cref="IEtkinYetkiServisi"/>'nden okunur — verilen yetki hemen geçerli olur, kaldırılan
/// yetki hemen kapanır (token ömrü beklenmez). Süper admin (K5) kullanıcı üzerindeki bayrakla
/// bypass eder; eski ölü <c>permission == "*"</c> dalı kaldırılmıştır.
///
/// Kanal kapsamı (K1/K2) bu katmanda DEĞİL, sorgu/işlem katmanında uygulanır (tasarım §C.2):
/// burada "en az bir kanalda yetkisi var mı" sorulur; kaydın kanalı Y3'te doğrulanır.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequirePermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _permission;

    /// <summary>Bu ucun istediği yetki key'i (kaplama testleri okur).</summary>
    public string Permission => _permission;

    public RequirePermissionAttribute(string permission) => _permission = permission;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Süper admin bayrağı token'dadır (kimlik bilgisi); tüm kontrolleri geçer.
        if (user.FindFirst("sa")?.Value == "true")
            return;

        var sub = user.FindFirst("sub")?.Value
                  ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(sub, out var userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var servis = context.HttpContext.RequestServices.GetService(typeof(IEtkinYetkiServisi)) as IEtkinYetkiServisi;
        if (servis is null)
        {
            // Servis kayıtlı değilse yetki AÇIK varsayılmaz (fail-safe kapalı).
            context.Result = new ObjectResult(new { success = false, error = "Yetki servisi kullanılamıyor." })
            { StatusCode = StatusCodes.Status503ServiceUnavailable };
            return;
        }

        var yetkiler = await servis.GetirAsync(userId, context.HttpContext.RequestAborted);
        if (yetkiler.Var(_permission))
            return;

        await YetkisizErisimLogu.YazAsync(context.HttpContext, "yetki", _permission, userId,
            StatusCodes.Status403Forbidden);

        context.Result = new ObjectResult(new { success = false, error = "Bu işlem için yetkiniz yok." })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}
