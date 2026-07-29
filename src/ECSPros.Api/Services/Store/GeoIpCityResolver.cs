using System.Net;
using MaxMind.GeoIP2;

namespace ECSPros.Api.Services.Store;

public interface IGeoIpCityResolver
{
    /// <summary>İstekteki istemci IP'sinden il plaka kodunu tahmin eder ("06" vb.).
    /// Veritabanı yapılandırılmamışsa, IP özel/yerelse, Türkiye dışıysa ya da
    /// çözümleme başarısızsa null döner — konum zinciri bir sonraki halkaya geçer.</summary>
    string? PlakaBul(HttpContext http);

    /// <summary>Açılış log'u için durum: veritabanı yüklendi mi.</summary>
    bool Aktif { get; }
}

/// <summary>
/// G9/H10: konum zincirinin 4. halkası — GeoLite2 City (MaxMind) yerel mmdb dosyasından
/// IP → il tahmini. ISO 3166-2:TR alt bölüm kodları plaka numarasıyla birebir olduğundan
/// ("TR-06" → "06") subdivision kodu doğrudan segment kimliğimizdir. Dosya yolu
/// `Store:GeoIp:DatabasePath` — dosya yoksa servis sessizce devre dışı kalır (site bozulmaz),
/// durum açılışta tek satır loglanır (Redis drift detektörü kalıbı). Reader thread-safe'tir,
/// singleton yaşar; IP tahmini kesin doğru sayılmaz (spec) — yalnız hiçbir halka dolu değilken.
/// </summary>
public sealed class GeoIpCityResolver : IGeoIpCityResolver, IDisposable
{
    private readonly DatabaseReader? _reader;
    private readonly ILogger<GeoIpCityResolver> _logger;

    public bool Aktif => _reader is not null;

    public GeoIpCityResolver(IConfiguration config, ILogger<GeoIpCityResolver> logger)
    {
        _logger = logger;
        var yol = config["Store:GeoIp:DatabasePath"];
        if (string.IsNullOrWhiteSpace(yol))
        {
            _logger.LogWarning("GeoIP: YAPILANDIRILMAMIŞ (Store:GeoIp:DatabasePath yok) — IP'den il tahmini kapalı, konum zinciri diğer halkalarla çalışıyor.");
            return;
        }
        try
        {
            if (!File.Exists(yol))
            {
                _logger.LogWarning("GeoIP: DOSYA BULUNAMADI ({Yol}) — IP'den il tahmini kapalı. GeoLite2-City.mmdb dosyasını bu yola koyup servisi yeniden başlatın.", yol);
                return;
            }
            _reader = new DatabaseReader(yol, MaxMind.Db.FileAccessMode.MemoryMapped);
            _logger.LogInformation("GeoIP: AKTİF ✓ ({Yol}, derleme {Tarih:yyyy-MM-dd})", yol, _reader.Metadata.BuildDate);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GeoIP: AÇILAMADI ({Yol}) — IP'den il tahmini kapalı.", yol);
        }
    }

    public string? PlakaBul(HttpContext http)
    {
        if (_reader is null) return null;
        try
        {
            var ip = IstemciIp(http);
            if (ip is null || !GercekDunyaIpsi(ip)) return null;
            if (!_reader.TryCity(ip, out var sehir) || sehir is null) return null;
            if (sehir.Country.IsoCode != "TR") return null;
            // ISO 3166-2:TR → "TR-06"; IsoCode alt bölüm kısmını ("06") verir = plaka.
            var plaka = sehir.Subdivisions.FirstOrDefault()?.IsoCode;
            return plaka is { Length: 2 } && plaka.All(char.IsAsciiDigit) ? plaka : null;
        }
        catch
        {
            // Tahmin halkası hiçbir isteği düşüremez — hata sessizce "bilinmiyor" olur.
            return null;
        }
    }

    /// <summary>Gerçek istemci IP'si — rate limiter'daki IstemciIpAnahtari ile aynı zincir
    /// (Program.cs): Cloudflare → nginx → app'te sırasıyla CF-Connecting-IP,
    /// X-Forwarded-For'un ilk halkası, soket adresi.</summary>
    private static IPAddress? IstemciIp(HttpContext http)
    {
        var cf = http.Request.Headers["CF-Connecting-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(cf) && IPAddress.TryParse(cf.Trim(), out var cfIp))
            return cfIp;
        var xff = http.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(xff) && IPAddress.TryParse(xff.Split(',')[0].Trim(), out var xffIp))
            return xffIp;
        return http.Connection.RemoteIpAddress;
    }

    /// <summary>Loopback/özel ağ/CGNAT adresleri mmdb'de yoktur; sorgulamadan elenir.</summary>
    private static bool GercekDunyaIpsi(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return false;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        var b = ip.GetAddressBytes();
        if (b.Length == 4)
            return !(b[0] == 10
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127) // CGNAT 100.64/10
                || (b[0] == 169 && b[1] == 254));
        // IPv6: link-local (fe80::/10) ve unique-local (fc00::/7) dışı kabul.
        return !(ip.IsIPv6LinkLocal || (b[0] & 0xFE) == 0xFC);
    }

    public void Dispose() => _reader?.Dispose();
}
