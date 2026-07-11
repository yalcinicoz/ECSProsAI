using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Api.Services.Store;

/// <summary>
/// G9: ziyaretçi segmenti — kural motorunun (G10), segment bazlı cache anahtarının (G11)
/// ve admin önizlemenin (G12) girdisi. Kural şemasındaki kimlikler: city = plaka kodu
/// ("06"), region = bölge adının kebab hâli ("ic-anadolu"), gender = male|female|unknown,
/// device = mobile|tablet|desktop, membership = member|guest, memberGroup = grup Guid'i.
/// </summary>
public sealed record VisitorSegment(
    string? CityCode,
    string? CityName,
    string? Region,
    string Gender,
    string Device,
    bool IsMember,
    Guid? MemberGroupId)
{
    public static readonly VisitorSegment Misafir = new(null, null, null, "unknown", "desktop", false, null);

    /// <summary>G11 cache anahtarı parçası: sehir:bolge:cinsiyet:cihaz:uyelik:grup (spec anahtar düzeni).</summary>
    public string CacheKey() =>
        $"{CityCode ?? "-"}:{Region ?? "-"}:{Gender}:{Device}:{(IsMember ? "member" : "guest")}:{(MemberGroupId?.ToString("N") ?? "-")}";

    /// <summary>G11: cache anahtarındaki kısa segment hash'i (spec: {segmentHash}) —
    /// CacheKey'in SHA256'sının ilk 16 hex karakteri (anahtar uzunluğu sınırlı kalır).</summary>
    public string CacheHash() =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(CacheKey())))[..16].ToLowerInvariant();
}

public interface IVisitorSegmentResolver
{
    /// <summary>İstekten segmenti çözer. memberId SSR cookie kimliğinden ya da bearer claim'den verilir (null = misafir).</summary>
    Task<VisitorSegment> ResolveAsync(HttpContext http, Guid? memberId, CancellationToken ct = default);

    /// <summary>G12: kurgu segment (admin önizlemesi) — plaka kodundan bölge/il adı çözülür,
    /// geçersiz/boş plaka konumsuz segment üretir; gender/device değerleri normalize edilir.</summary>
    Task<VisitorSegment> BuildAsync(
        string? cityCode, string? gender, string? device, bool isMember, Guid? memberGroupId,
        CancellationToken ct = default);
}

/// <summary>
/// Konum zinciri (plan G9 kararı, sırayla): 1) üyenin varsayılan teslimat adresi şehri →
/// 2) üye profil şehri → 3) manuel seçim cookie'si `ms_sehir` (plaka kodu; nav şehir çipi
/// ve "Konumumu kullan" tarayıcı konumu bunu yazar — sayfa açılışında izin pop-up'ı YOK) →
/// 4) IP tahmini yuvası (GeoLite2 mmdb dosyası edinilince bağlanır — ileri iş) →
/// 5) bilinmiyor. IP tahmini kesin doğru sayılmaz; cinsiyet YALNIZ profilden, tahmin yok (spec).
/// </summary>
public class VisitorSegmentResolver(IMemberService memberService, ICrmDbContext crm, IMemoryCache cache)
    : IVisitorSegmentResolver
{
    public const string SehirCookie = "ms_sehir";

    /// <summary>Anonim endpoint'lerde opsiyonel üye bağlamı: bearer claim'lerinden üye Id'si
    /// (type=member değilse ya da token yoksa null — misafir). Segment/pages controller'ları paylaşır.</summary>
    public static Guid? MemberIdFromClaims(System.Security.Claims.ClaimsPrincipal user)
    {
        if (user.FindFirst("type")?.Value != "member") return null;
        return Guid.TryParse(user.FindFirst("sub")?.Value
            ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id)
            ? id : null;
    }

    public async Task<VisitorSegment> ResolveAsync(HttpContext http, Guid? memberId, CancellationToken ct = default)
    {
        var iller = await IlleriGetirAsync(ct);
        var device = CihazBul(http.Request.Headers.UserAgent.ToString());

        MemberInfo? uye = null;
        if (memberId is { } id)
            uye = await memberService.GetMemberAsync(id, ct);

        // Konum zinciri
        string? cityCode = null;
        if (uye?.DefaultAddressCityId is { } adresSehir && iller.ById.TryGetValue(adresSehir, out var il1))
            cityCode = il1.Code;
        else if (uye?.CityId is { } profilSehir && iller.ById.TryGetValue(profilSehir, out var il2))
            cityCode = il2.Code;
        else if (http.Request.Cookies.TryGetValue(SehirCookie, out var manuel)
                 && manuel is { Length: 2 } && iller.ByCode.ContainsKey(manuel))
            cityCode = manuel;
        // 4) IP tahmini yuvası: GeoLite2 mmdb edinilince buraya bağlanır (ileri iş)

        string? region = null, cityName = null;
        if (cityCode is not null && iller.ByCode.TryGetValue(cityCode, out var il))
            (region, cityName) = (il.Region, il.Name);

        var gender = uye?.Gender?.ToLowerInvariant() switch
        {
            "male" or "erkek" or "m" => "male",
            "female" or "kadin" or "kadın" or "f" => "female",
            _ => "unknown",
        };

        return new VisitorSegment(cityCode, cityName, region, gender, device, uye is not null, uye?.MemberGroupId);
    }

    public async Task<VisitorSegment> BuildAsync(
        string? cityCode, string? gender, string? device, bool isMember, Guid? memberGroupId,
        CancellationToken ct = default)
    {
        string? kod = null, ad = null, bolge = null;
        if (cityCode is { Length: 2 })
        {
            var iller = await IlleriGetirAsync(ct);
            if (iller.ByCode.TryGetValue(cityCode, out var il))
                (kod, ad, bolge) = (cityCode, il.Name, il.Region);
        }
        var cinsiyet = gender?.ToLowerInvariant() is "male" or "female" ? gender!.ToLowerInvariant() : "unknown";
        var cihaz = device?.ToLowerInvariant() is "mobile" or "tablet" ? device!.ToLowerInvariant() : "desktop";
        return new VisitorSegment(kod, ad, bolge, cinsiyet, cihaz, isMember, isMember ? memberGroupId : null);
    }

    /// <summary>UA sınıflaması — tablet önce denenir (Android tablet UA'sında "Mobile" geçmez).</summary>
    private static string CihazBul(string ua)
    {
        if (string.IsNullOrEmpty(ua)) return "desktop";
        if (ua.Contains("iPad", StringComparison.OrdinalIgnoreCase)
            || (ua.Contains("Android", StringComparison.OrdinalIgnoreCase)
                && !ua.Contains("Mobile", StringComparison.OrdinalIgnoreCase))
            || ua.Contains("Tablet", StringComparison.OrdinalIgnoreCase))
            return "tablet";
        if (ua.Contains("Mobi", StringComparison.OrdinalIgnoreCase)
            || ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase))
            return "mobile";
        return "desktop";
    }

    private sealed record IlBilgisi(string Code, string? Name, string? Region);
    private sealed record IlHaritasi(Dictionary<Guid, IlBilgisi> ById, Dictionary<string, IlBilgisi> ByCode);

    /// <summary>81 il — kod + kebab bölge; süreç içi 1 saat cache (referans veri, değişmez).</summary>
    private async Task<IlHaritasi> IlleriGetirAsync(CancellationToken ct)
    {
        return (await cache.GetOrCreateAsync("segment-iller", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            var sehirler = await crm.Cities.AsNoTracking()
                .Where(c => c.IsActive)
                .Select(c => new { c.Id, c.Code, c.NameI18n, c.Region })
                .ToListAsync(ct);
            var byId = new Dictionary<Guid, IlBilgisi>();
            var byCode = new Dictionary<string, IlBilgisi>();
            foreach (var s in sehirler)
            {
                var ad = s.NameI18n.TryGetValue("tr", out var tr) ? tr : s.NameI18n.Values.FirstOrDefault();
                var bilgi = new IlBilgisi(s.Code, ad, Kebab(s.Region));
                byId[s.Id] = bilgi;
                byCode[s.Code] = bilgi;
            }
            return new IlHaritasi(byId, byCode);
        }))!;
    }

    /// <summary>"İç Anadolu" → "ic-anadolu" (kural şemasının stabil ASCII kimliği).
    /// Karakter haritası büyük/küçük iki hâli de kapsar — "İ".ToLowerInvariant()'ın
    /// birleşik noktalı i (i + U+0307) üretmesi güvenilmezdi (admin toSlug deseni).</summary>
    private static string? Kebab(string? metin)
    {
        if (string.IsNullOrWhiteSpace(metin)) return null;
        var harita = new Dictionary<char, char>
        {
            ['ç'] = 'c', ['Ç'] = 'c', ['ğ'] = 'g', ['Ğ'] = 'g', ['ı'] = 'i', ['İ'] = 'i',
            ['ö'] = 'o', ['Ö'] = 'o', ['ş'] = 's', ['Ş'] = 's', ['ü'] = 'u', ['Ü'] = 'u',
        };
        var sonuc = new System.Text.StringBuilder();
        foreach (var ch in metin.Trim())
        {
            var duz = harita.TryGetValue(ch, out var esle) ? esle : char.ToLowerInvariant(ch);
            if (duz is >= 'a' and <= 'z' or >= '0' and <= '9') sonuc.Append(duz);
            else if (sonuc.Length > 0 && sonuc[^1] != '-') sonuc.Append('-');
        }
        return sonuc.ToString().Trim('-');
    }
}
