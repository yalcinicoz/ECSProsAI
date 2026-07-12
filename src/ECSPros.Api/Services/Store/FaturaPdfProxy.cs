using System.Net;
using System.Text.RegularExpressions;

namespace ECSPros.Api.Services.Store;

/// <summary>
/// H1: Entegratör e-arşiv fatura sayfasından PDF'i çekip döner (misharix FaturaController
/// mantığının taşınmış hâli). Fark: URL istemciden gelmez — çağıran, sahipliği doğrulanmış
/// Invoice.IntegratorInvoiceUrl'i verir; burada yalnız allowlist + içerik doğrulaması yapılır.
/// Allowlist config'ten: Store:InvoiceProxy:AllowedHosts (+ AllowedPathPrefix, vars. /earchive/).
/// </summary>
public interface IFaturaPdfProxy
{
    Task<FaturaPdfSonucu> GetirAsync(string faturaUrl, CancellationToken ct);
}

/// <summary>Başarıda Pdf dolu; hatada HataKodu (HTTP durum önerisi) + kullanıcıya uygun mesaj.</summary>
public record FaturaPdfSonucu(byte[]? Pdf, int HataKodu = 0, string? HataMesaji = null)
{
    public bool Basarili => Pdf is not null;
}

public class FaturaPdfProxy(IConfiguration configuration, ILogger<FaturaPdfProxy> logger) : IFaturaPdfProxy
{
    // Entegratör görüntüleme sayfası PDF'i <object data="..."> ile gömer — adresi buradan çekeriz.
    private static readonly Regex PdfNesneRegex =
        new("data\\s*=\\s*[\"'](?<url>[^\"']+)[\"']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<FaturaPdfSonucu> GetirAsync(string faturaUrl, CancellationToken ct)
    {
        if (!Uri.TryCreate(faturaUrl, UriKind.Absolute, out var faturaUri) || !HostIzinliMi(faturaUri))
        {
            logger.LogWarning("Fatura PDF isteği allowlist dışı adrese düştü: {Url}", faturaUrl);
            return new FaturaPdfSonucu(null, StatusCodes.Status502BadGateway, "Fatura adresi izinli servislerde değil.");
        }

        var yolOnEki = configuration["Store:InvoiceProxy:AllowedPathPrefix"] ?? "/earchive/";
        if (!faturaUri.AbsolutePath.StartsWith(yolOnEki, StringComparison.OrdinalIgnoreCase))
            return new FaturaPdfSonucu(null, StatusCodes.Status502BadGateway, "Fatura adresi izinli servislerde değil.");

        // İstek başına taze istemci (kaynak desen): entegratör oturum cookie'leri istekler
        // arasında paylaşılmaz; çağrı seyrek olduğundan handler havuzu gerekmiyor.
        var cookieKutusu = new CookieContainer();
        using var istemciHandler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            CookieContainer = cookieKutusu,
            AutomaticDecompression = DecompressionMethods.All
        };
        using var istemci = new HttpClient(istemciHandler) { Timeout = TimeSpan.FromSeconds(20) };
        istemci.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 ECSProsFaturaOnizleme/1.0");

        try
        {
            using var faturaSayfaCevabi = await istemci.GetAsync(faturaUri, ct);
            if (!faturaSayfaCevabi.IsSuccessStatusCode)
                return new FaturaPdfSonucu(null, (int)faturaSayfaCevabi.StatusCode, "Fatura servisine ulaşılamadı.");

            if (PdfMi(faturaSayfaCevabi.Content.Headers.ContentType?.MediaType))
                return new FaturaPdfSonucu(await faturaSayfaCevabi.Content.ReadAsByteArrayAsync(ct));

            var faturaSayfaHtml = await faturaSayfaCevabi.Content.ReadAsStringAsync(ct);
            var pdfNesneEslesmesi = PdfNesneRegex.Match(faturaSayfaHtml);
            if (!pdfNesneEslesmesi.Success)
                return new FaturaPdfSonucu(null, StatusCodes.Status502BadGateway, "Fatura PDF adresi bulunamadı.");

            var pdfUri = new Uri(faturaUri, WebUtility.HtmlDecode(pdfNesneEslesmesi.Groups["url"].Value));
            if (!HostIzinliMi(pdfUri))
                return new FaturaPdfSonucu(null, StatusCodes.Status502BadGateway, "Fatura PDF adresi izinli servislerde değil.");

            using var pdfCevabi = await istemci.GetAsync(pdfUri, ct);
            if (!pdfCevabi.IsSuccessStatusCode)
                return new FaturaPdfSonucu(null, (int)pdfCevabi.StatusCode, "Fatura PDF'i alınamadı.");

            if (!PdfMi(pdfCevabi.Content.Headers.ContentType?.MediaType))
                return new FaturaPdfSonucu(null, StatusCodes.Status502BadGateway, "Fatura PDF olarak alınamadı.");

            return new FaturaPdfSonucu(await pdfCevabi.Content.ReadAsByteArrayAsync(ct));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Fatura PDF proxy hatası: {Host}", faturaUri.Host);
            return new FaturaPdfSonucu(null, StatusCodes.Status502BadGateway, "Fatura servisine ulaşılamadı.");
        }
    }

    private bool HostIzinliMi(Uri uri)
    {
        if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;

        var izinliler = configuration.GetSection("Store:InvoiceProxy:AllowedHosts").Get<string[]>() ?? [];
        return izinliler.Any(h => uri.Host.Equals(h, StringComparison.OrdinalIgnoreCase));
    }

    private static bool PdfMi(string? mediaType) =>
        mediaType?.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) == true;
}
