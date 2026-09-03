using ImageMagick;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Api.Services.Store;

/// <summary>
/// PageSpeed A fazı (2026-07-30): vitrin görsellerinden sabit genişlik merdiveninde
/// WebP varyantları üretir (orijinal dosyaya dokunulmaz). Storefront render'ı varyantı
/// olan görsellere srcset/sizes basar — cihaz hangi genişliğe ihtiyaç duyuyorsa onu indirir.
/// Personel ne yüklerse yüklesin platform normalize eder; "doğru boyut" yükleme sorumluluğu
/// personelden kalkar (docs/SiteYavaslikDegerlendirme değerlendirmesinin devamı).
/// </summary>
public static class VitrinGorselVaryantlari
{
    /// <summary>Üretilen genişlikler — slot ölçümleriyle uyumlu (403px desktop kutu,
    /// ~1050px retina mobil, 1236px hero, 2x hero).</summary>
    public static readonly int[] Genislikler = [480, 800, 1200, 1920];

    /// <summary>Varyant üretilebilen içerik tipleri (SVG vektör, GIF animasyon — atlanır).</summary>
    public static bool Desteklenir(string contentType) =>
        contentType is "image/jpeg" or "image/png" or "image/webp";

    public static string VaryantDosyaAdi(string dosyaAdi, int genislik) =>
        $"{Path.GetFileNameWithoutExtension(dosyaAdi)}_w{genislik}.webp";

    /// <summary>Orijinal dosyanın yanına, orijinalden DAR olan her merdiven genişliği için
    /// _w{N}.webp üretir. Var olan varyantın üzerine yazar (yeniden yükleme senaryosu).</summary>
    public static async Task UretAsync(string dosyaYolu, CancellationToken ct = default)
    {
        var dizin = Path.GetDirectoryName(dosyaYolu)!;
        using var gorsel = new MagickImage(dosyaYolu);
        foreach (var genislik in Genislikler.Where(g => g < gorsel.Width))
        {
            using var kopya = (MagickImage)gorsel.Clone();
            kopya.Resize(new MagickGeometry((uint)genislik, 0)); // yükseklik orandan
            kopya.Quality = 78;
            kopya.Format = MagickFormat.WebP;
            await kopya.WriteAsync(
                Path.Combine(dizin, VaryantDosyaAdi(dosyaYolu, genislik)), ct);
        }
    }
}

/// <summary>
/// Render tarafı: /media görsel URL'inden, diskte gerçekten var olan varyantlara göre
/// srcset dizesi üretir. Dosya sistemi kontrolü kısa süreli cache'lenir (eski görsellerde
/// varyant yoktur — srcset basılmaz, davranış bugünkü gibi kalır; 404 riski oluşmaz).
/// </summary>
public class VitrinSrcsetSaglayici(IConfiguration configuration, IMemoryCache cache)
{
    private string MediaKok => configuration["Store:MediaRootPath"] ?? "/opt/ECSProsAI/media";
    private string StorefrontCdnKok =>
        (configuration["StorefrontMediaStorage:PublicBaseUrl"] ??
         "https://cdn.misharitalia.com/storefront-v1").TrimEnd('/');
    private bool CdnVaryantlariEtkin =>
        configuration.GetValue("StorefrontMediaStorage:ResponsiveVariantsEnabled", false);

    /// <summary>Görselin gerçek piksel boyutları (dosya başlığından, cache'li) — img
    /// width/height öznitelikleri GERÇEK oranla basılırsa tarayıcının ayırdığı alan
    /// yükleme sonrası değişmez → CLS sıfırlanır. Sabit şablon oranı (110x165 vb.)
    /// personelin yüklediği gerçek oranla eşleşmeyince kayma üretiyordu (2026-07-30 A/B).</summary>
    public (int Genislik, int Yukseklik)? Boyut(string? gorselUrl)
    {
        if (string.IsNullOrEmpty(gorselUrl) || !gorselUrl.StartsWith("/media/", StringComparison.Ordinal))
            return null;
        return cache.GetOrCreate("vitrin-boyut:" + gorselUrl, girdi =>
        {
            girdi.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            try
            {
                var tamYol = Path.Combine(MediaKok,
                    gorselUrl["/media/".Length..].Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(tamYol)) return ((int, int)?)null;
                var bilgi = new MagickImageInfo(tamYol); // yalnız başlık okur, piksel verisi yüklemez
                return bilgi.Width > 0 && bilgi.Height > 0
                    ? ((int)bilgi.Width, (int)bilgi.Height)
                    : ((int, int)?)null;
            }
            catch { return ((int, int)?)null; } // SVG/bozuk dosya → şablon sabitine düşülür
        });
    }

    /// <summary>Varyantlar varsa "url_w480.webp 480w, ..." dizesi; yoksa null.</summary>
    public string? Srcset(string? gorselUrl)
    {
        if (CdnVaryantlariEtkin && IsStorefrontCdnRaster(gorselUrl))
        {
            var cdnUrl = gorselUrl!;
            var urlDizin = cdnUrl[..cdnUrl.LastIndexOf('/')];
            return string.Join(", ", VitrinGorselVaryantlari.Genislikler.Select(genislik =>
                $"{urlDizin}/{VitrinGorselVaryantlari.VaryantDosyaAdi(cdnUrl, genislik)} {genislik}w"));
        }
        if (string.IsNullOrEmpty(gorselUrl) || !gorselUrl.StartsWith("/media/", StringComparison.Ordinal))
            return null;
        return cache.GetOrCreate("vitrin-srcset:" + gorselUrl, girdi =>
        {
            girdi.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            var goreceliYol = gorselUrl["/media/".Length..].Replace('/', Path.DirectorySeparatorChar);
            var tamYol = Path.Combine(MediaKok, goreceliYol);
            var dizin = Path.GetDirectoryName(tamYol);
            var urlDizin = gorselUrl[..gorselUrl.LastIndexOf('/')];
            if (dizin is null) return null;

            var parcalar = new List<string>();
            foreach (var genislik in VitrinGorselVaryantlari.Genislikler)
            {
                var ad = VitrinGorselVaryantlari.VaryantDosyaAdi(tamYol, genislik);
                if (File.Exists(Path.Combine(dizin, ad)))
                    parcalar.Add($"{urlDizin}/{ad} {genislik}w");
            }
            return parcalar.Count == 0 ? null : string.Join(", ", parcalar);
        });
    }

    private bool IsStorefrontCdnRaster(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !url.StartsWith(StorefrontCdnKok + "/", StringComparison.OrdinalIgnoreCase))
            return false;
        var extension = Path.GetExtension(Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri.AbsolutePath
            : url);
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }
}
