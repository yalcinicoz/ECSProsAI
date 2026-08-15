namespace ECSPros.Api.Services;

/// <summary>
/// Arama motoru / sosyal medya tarayıcılarını (Googlebot, meta-externalagent, Bingbot…)
/// İLGİLENDİRMEYEN vitrin yolları — TEK KAYNAK (2026-08-15). Üç yerde birden kullanılır:
///  1) /robots.txt buradan ÜRETİLİR (wwwroot'ta statik dosya yok),
///  2) <see cref="XRobotsTagMiddleware"/> bu yollara "X-Robots-Tag: noindex, nofollow" başlığı basar,
///  3) Razor'daki bu yollara giden linklerde rel="nofollow" (Views; el ile eşlenir).
/// Neden: 2026-08-15 gecesi Meta crawler ürün kartlarındaki /benzer linklerini ~8.700 kez
/// gezip görsel arama sunucumuzu yordu. Sepet/ödeme/hesabım/favori/koleksiyon gibi yalnız
/// üye etkileşimi olan sayfaların indekste/tarama bütçesinde işi yok.
/// Kural: yeni bir üye-etkileşim sayfası açıldığında yolunu BURAYA ekle.
/// </summary>
public static class BotDisiRotalar
{
    /// <summary>Yol ÖNEKLERİ (büyük/küçük harf duyarsız eşleşir; "/o/" gibi sonu bölü ile
    /// biten önek yalnız alt yolları yakalar).</summary>
    public static readonly string[] Onekler =
    [
        // Ürün kartı etkileşimleri / dış servis tetikleyen sayfalar
        "/benzer",                    // benzer ürünler (görsel arama sunucumuza gider)
        "/gorsel-arama",              // görsel arama POST
        // Sepet → ödeme akışı
        "/sepet", "/teslimat", "/odeme", "/odeme-sonuc", "/siparis-tamamlandi",
        "/CheckoutPayment",           // PayTR bildirim/geri dönüş
        "/o/",                        // sipariş onay linki /o/{token}
        // Üye alanı (kısa ve /Hesabim/... biçimleri)
        "/hesabim", "/hesabim-varsayilan", "/uyelik-bilgilerim", "/adreslerim",
        "/siparislerim", "/favorilerim", "/favori-aramalarim", "/iadelerim",
        "/indirim-kuponlarim", "/koleksiyonlarim", "/onceden-gezdiklerim",
        "/tekrar-satin-al", "/yorumlarim",
        // Üyeliksiz kargo takibi (kişisel sorgu formu)
        "/uyeliksiz-kargo-takip",
        // Teknik / iç yüzeyler
        "/api", "/hubs", "/agent", "/ProjeElementleri", "/onizleme", "/yazdir",
        "/hata", "/swagger", "/admin", "/satici", "/store",
    ];

    /// <summary>Sorgu dizesi kalıpları — yalnız robots.txt için (site içi arama sonuçları).</summary>
    public static readonly string[] SorguKaliplari = ["/*?search=", "/*&search="];

    public static bool BotDisiMi(PathString path)
    {
        var p = path.Value;
        if (string.IsNullOrEmpty(p) || p == "/") return false;
        foreach (var onek in Onekler)
        {
            if (!p.StartsWith(onek, StringComparison.OrdinalIgnoreCase)) continue;
            // "/sepet" → "/sepetim" gibi yanlış pozitifi engelle: önek tam yol ya da bölü ile devam etmeli
            if (onek.EndsWith('/') || p.Length == onek.Length || p[onek.Length] == '/' || p[onek.Length] == '?')
                return true;
        }
        return false;
    }

    /// <summary>robots.txt içeriği (statik dosya yerine bu üretilir).</summary>
    public static string RobotsTxt()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("User-agent: *\n");
        sb.Append("Allow: /\n");
        foreach (var onek in Onekler)
        {
            sb.Append("Disallow: ").Append(onek).Append('\n');
            // robots.txt büyük/küçük harf DUYARLI — küçük harfli varyantı da yaz
            var kucuk = onek.ToLowerInvariant();
            if (kucuk != onek) sb.Append("Disallow: ").Append(kucuk).Append('\n');
        }
        foreach (var k in SorguKaliplari)
            sb.Append("Disallow: ").Append(k).Append('\n');
        return sb.ToString();
    }
}

/// <summary>Bot-dışı yollara "X-Robots-Tag: noindex, nofollow" basar (robots.txt'yi
/// dinlemeyen ama başlığı sayan tarayıcılar için ikinci kilit).</summary>
public sealed class XRobotsTagMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext ctx)
    {
        if (BotDisiRotalar.BotDisiMi(ctx.Request.Path))
            ctx.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        return next(ctx);
    }
}
