using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ECSPros.Api.Authorization;

/// <summary>
/// Y3 3. tur (2026-09-09, karar K2): KANAL PARAMETRELİ ekranların kapsam denetimi.
///
/// Vitrin, menü, kanal ürünleri, CMS, pazaryeri gibi ekranlar liste filtrelemez — tek bir kanalla
/// çalışır ve kanalı istekte taşır (<c>firmPlatformId</c>). Bu filtre, model bağlandıktan sonra
/// aksiyonun argümanlarında kanal kimliğini arar ve kullanıcının o kanala erişimi yoksa isteği
/// <b>404</b> ile keser (403 değil: kanalın/kaydın varlığı sızmasın, tasarım §C.2).
///
/// Kanal parametresi taşımayan aksiyonlar etkilenmez; süper adminde ve kanal kapsamsız yetkide
/// denetim yapılmaz. Sınıf düzeyinde bir kez konur, controller'ın tüm uçlarını kapsar.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class KanalKapsamiKontrolAttribute : Attribute, IAsyncActionFilter
{
    /// <summary>Kapsamın okunacağı yetki (kanal kümesi bu yetkiden gelir).</summary>
    public string Permission { get; }

    /// <summary>Aranan argüman adları — panelde standart ad <c>firmPlatformId</c>.</summary>
    private static readonly string[] Adaylar = ["firmPlatformId", "platformId", "channelId"];

    public KanalKapsamiKontrolAttribute(string permission) => Permission = permission;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var kapsam = context.HttpContext.RequestServices.GetService(typeof(IKanalKapsami)) as IKanalKapsami;
        if (kapsam is null)
        {
            context.Result = new ObjectResult(new { success = false, error = "Yetki servisi kullanılamıyor." })
            { StatusCode = StatusCodes.Status503ServiceUnavailable };
            return;
        }

        foreach (var ad in Adaylar)
        {
            if (!context.ActionArguments.TryGetValue(ad, out var deger) || deger is null) continue;
            // Guid? kutulandığında tip Guid olur; tek desen yeterli.
            var kanal = deger is Guid g ? g : Guid.Empty;
            if (kanal == Guid.Empty) continue;   // kanal verilmemiş → uç kendi varsayılanıyla çalışır

            if (!await kapsam.ErisebilirMiAsync(Permission, kanal, context.HttpContext.RequestAborted))
            {
                await YetkisizErisimLogu.YazAsync(context.HttpContext, "kanal", Permission,
                    durum: StatusCodes.Status404NotFound);
                context.Result = new NotFoundObjectResult(new { success = false, error = "Kayıt bulunamadı." });
                return;
            }
        }

        await next();
    }
}
