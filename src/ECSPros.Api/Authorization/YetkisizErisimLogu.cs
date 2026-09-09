using ECSPros.Iam.Application.Yetkilendirme;

namespace ECSPros.Api.Authorization;

/// <summary>
/// Y8 (2026-09-09, §J.1): ret noktalarının tek çağrı yeri.
///
/// Kural: <b>log yazmak isteği bozmaz.</b> Denetim kaydı yazılamazsa (DB kısa süreli
/// erişilemez, istek iptal edildi…) istek yine de reddedilir — yani hata yutulur ve
/// yalnız uygulama loguna düşer. Reddin kendisi bu yüzden asla kaybolmaz.
///
/// Anonim istek (401) yazılmaz: aktörü yoktur, gürültüden başka bir şey üretmez.
/// </summary>
public static class YetkisizErisimLogu
{
    public static async Task YazAsync(HttpContext http, string tur, string yetkiKey,
        Guid? kullaniciId = null, int durum = 403)
    {
        try
        {
            var id = kullaniciId ?? KullaniciId(http);
            if (id == Guid.Empty) return;                    // anonim → yazma

            if (http.RequestServices.GetService(typeof(IYetkisizErisimKaydedici))
                is not IYetkisizErisimKaydedici kaydedici) return;

            await kaydedici.DeneAsync(new YetkisizErisimKaydi(
                tur, yetkiKey, http.Request.Path.Value ?? "", http.Request.Method, durum, 0),
                id, http.RequestAborted);
        }
        catch (Exception ex)
        {
            // Denetim kaydı yazılamadı: reddi ETKİLEMEZ, yalnız uygulama loguna düşer.
            (http.RequestServices.GetService(typeof(ILoggerFactory)) as ILoggerFactory)?
                .CreateLogger("YetkisizErisimLogu")
                .LogWarning(ex, "Yetkisiz erişim denemesi kaydedilemedi ({Yol}).", http.Request.Path);
        }
    }

    public static Guid KullaniciId(HttpContext http)
    {
        var sub = http.User.FindFirst("sub")?.Value
                  ?? http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }
}
