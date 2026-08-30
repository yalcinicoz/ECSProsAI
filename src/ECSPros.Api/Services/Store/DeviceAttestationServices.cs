using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace ECSPros.Api.Services.Store;

/// <summary>
/// Mobil cihaz doğrulama altyapısı (2026-07-23, kullanıcı kararı):
/// APK/IPA'ya sabit secret GÖMÜLMEZ. Uygulama açılışta platform attestation'ı
/// (Android: Play Integrity, iOS: App Attest) ile kendini kanıtlar; sunucu karşılığında
/// KISA ÖMÜRLÜ (varsayılan 15 dk) anonim device token + o oturuma özel imza secret'ı üretir.
/// Secret sunucu üretimidir ve yalnız attestation geçen istemciye verilir — pakete gömülü
/// hiçbir şey yoktur. Her istek timestamp+nonce+HMAC imzasıyla gelir (replay reddi
/// DeviceRequestGuardMiddleware'de).
/// </summary>
public sealed record AttestationSonucu(bool Basarili, string? Hata = null)
{
    public static AttestationSonucu Basarisiz(string hata) => new(false, hata);
    public static readonly AttestationSonucu Gecti = new(true);
}

public interface IDeviceAttestationVerifier
{
    /// <summary>"android" | "ios"</summary>
    string Platform { get; }
    Task<AttestationSonucu> VerifyAsync(string attestation, string challenge, CancellationToken ct);
}

/// <summary>
/// Android Play Integrity: uygulamanın gönderdiği integrity token'ı Google'ın
/// decodeIntegrityToken API'sinde çözer; paket adı + PLAY_RECOGNIZED + MEETS_DEVICE_INTEGRITY
/// + challenge (nonce) eşleşmesini denetler. Config (uygulama yayınlanınca doldurulur):
///   MobileAttestation:Android:PackageName
///   MobileAttestation:Android:ServiceAccountJsonPath (GCP servis hesabı anahtarı)
/// </summary>
public sealed class PlayIntegrityVerifier(
    IConfiguration config,
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    ILogger<PlayIntegrityVerifier> logger) : IDeviceAttestationVerifier
{
    public string Platform => "android";

    public async Task<AttestationSonucu> VerifyAsync(string attestation, string challenge, CancellationToken ct)
    {
        var paketAdi = config["MobileAttestation:Android:PackageName"];
        var anahtarYolu = config["MobileAttestation:Android:ServiceAccountJsonPath"];
        if (string.IsNullOrWhiteSpace(paketAdi) || string.IsNullOrWhiteSpace(anahtarYolu) || !File.Exists(anahtarYolu))
            return AttestationSonucu.Basarisiz(
                "Play Integrity doğrulaması henüz yapılandırılmadı (paket adı + GCP servis hesabı gerekli).");

        try
        {
            var erisimToken = await GoogleErisimTokeniAlAsync(anahtarYolu, ct);
            var http = httpClientFactory.CreateClient("play-integrity");
            using var istek = new HttpRequestMessage(HttpMethod.Post,
                $"https://playintegrity.googleapis.com/v1/{paketAdi}:decodeIntegrityToken");
            istek.Headers.Authorization = new("Bearer", erisimToken);
            istek.Content = new StringContent(
                JsonSerializer.Serialize(new { integrity_token = attestation }),
                Encoding.UTF8, "application/json");

            using var yanit = await http.SendAsync(istek, ct);
            var govde = await yanit.Content.ReadAsStringAsync(ct);
            if (!yanit.IsSuccessStatusCode)
            {
                logger.LogWarning("Play Integrity decode başarısız: {Status} {Body}", yanit.StatusCode, govde);
                return AttestationSonucu.Basarisiz("Integrity token çözülemedi.");
            }

            using var json = JsonDocument.Parse(govde);
            var payload = json.RootElement.GetProperty("tokenPayloadExternal");

            var istekDetay = payload.GetProperty("requestDetails");
            if (istekDetay.GetProperty("requestPackageName").GetString() != paketAdi)
                return AttestationSonucu.Basarisiz("Paket adı eşleşmiyor.");
            // Uygulama, /device/challenge'dan aldığı nonce'u integrity isteğine koyar —
            // tekrar oynatılan attestation'lar burada düşer.
            var gelenNonce = istekDetay.TryGetProperty("nonce", out var n) ? n.GetString() : null;
            if (gelenNonce != challenge)
                return AttestationSonucu.Basarisiz("Challenge eşleşmiyor.");

            var uygulama = payload.GetProperty("appIntegrity").GetProperty("appRecognitionVerdict").GetString();
            if (uygulama != "PLAY_RECOGNIZED")
                return AttestationSonucu.Basarisiz("Uygulama Play tarafından tanınmadı (resmi mağaza sürümü değil).");

            var cihazEtiketleri = payload.GetProperty("deviceIntegrity")
                .GetProperty("deviceRecognitionVerdict").EnumerateArray()
                .Select(e => e.GetString()).ToHashSet();
            if (!cihazEtiketleri.Contains("MEETS_DEVICE_INTEGRITY"))
                return AttestationSonucu.Basarisiz("Cihaz bütünlük denetiminden geçemedi.");

            return AttestationSonucu.Gecti;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Play Integrity doğrulama hatası");
            return AttestationSonucu.Basarisiz("Doğrulama sırasında hata oluştu.");
        }
    }

    /// <summary>GCP servis hesabı anahtarıyla playintegrity kapsamlı OAuth erişim token'ı
    /// (1 saat önbelleklenir — Google SDK'sız, RS256 imzalı JWT değişimi).</summary>
    private async Task<string> GoogleErisimTokeniAlAsync(string anahtarYolu, CancellationToken ct)
    {
        var mevcut = cache.Get<string>("play-integrity-oauth");
        if (mevcut is not null) return mevcut;

        using var anahtarJson = JsonDocument.Parse(await File.ReadAllTextAsync(anahtarYolu, ct));
        var eposta = anahtarJson.RootElement.GetProperty("client_email").GetString()!;
        var pem = anahtarJson.RootElement.GetProperty("private_key").GetString()!;

        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        var imzaci = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
        var simdi = DateTime.UtcNow;
        var jwt = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: eposta,
            audience: "https://oauth2.googleapis.com/token",
            claims: [new Claim("scope", "https://www.googleapis.com/auth/playintegrity")],
            notBefore: simdi,
            expires: simdi.AddMinutes(60),
            signingCredentials: imzaci));

        var http = httpClientFactory.CreateClient("play-integrity");
        using var yanit = await http.PostAsync("https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = jwt,
            }), ct);
        yanit.EnsureSuccessStatusCode();
        using var tokenJson = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync(ct));
        var erisim = tokenJson.RootElement.GetProperty("access_token").GetString()!;
        cache.Set("play-integrity-oauth", erisim, TimeSpan.FromMinutes(50));
        return erisim;
    }
}

/// <summary>
/// iOS App Attest — FAZ 2: doğrulama CBOR/sertifika zinciri ister ve Apple bundle/team ID
/// olmadan kurulamaz; iOS uygulaması yayına hazırlanırken tamamlanacak. O güne dek iOS
/// istemciler reddedilir (bilinçli — "şimdilik açık bırak" güvenlik açığı olurdu).
/// </summary>
public sealed class AppAttestVerifier : IDeviceAttestationVerifier
{
    public string Platform => "ios";
    public Task<AttestationSonucu> VerifyAsync(string attestation, string challenge, CancellationToken ct) =>
        Task.FromResult(AttestationSonucu.Basarisiz(
            "App Attest doğrulaması henüz yapılandırılmadı (iOS uygulama kimliği bekleniyor)."));
}

/// <summary>
/// Mobil geliştirme köprüsü: YALNIZ MobileAttestation:DevBypassSecret config'i doluysa
/// devrededir ve attestation payload'ı bu değere eşitse geçirir. Prod appsettings'e ASLA
/// yazılmaz (test/geliştirme ortamında env var ile verilir) — uygulama mağaza kimliği
/// alana kadar mobil ekibin entegrasyonu bloke olmasın diye vardır.
/// </summary>
public sealed class DevBypassVerifier(IConfiguration config) : IDeviceAttestationVerifier
{
    public string Platform => "*";
    public bool Aktif => !string.IsNullOrWhiteSpace(config["MobileAttestation:DevBypassSecret"]);

    public Task<AttestationSonucu> VerifyAsync(string attestation, string challenge, CancellationToken ct) =>
        Task.FromResult(Aktif && attestation == config["MobileAttestation:DevBypassSecret"]
            ? AttestationSonucu.Gecti
            : AttestationSonucu.Basarisiz("Geçersiz attestation."));
}

public sealed record DeviceTokenSonucu(string DeviceToken, string SigningSecret, DateTime ExpiresAt);

/// <summary>
/// Attestation geçen istemciye kısa ömürlü anonim device JWT + oturuma özel HMAC secret'ı
/// üretir. FAZ 10 / A3: secret artık IDeviceStateStore'da (Redis) — attestation A düğümünde,
/// imzalı istek B düğümünde doğrulanabilir; Redis yoksa FAIL-CLOSED. İstek imzaları
/// DeviceRequestGuardMiddleware'de bu secret'la denetlenir. Token type=device claim'i taşır —
/// MemberOnly/AdminOnly policy'lerinden geçemez.
/// </summary>
public interface IDeviceTokenService
{
    Task<DeviceTokenSonucu> TokenUretAsync(string platform);
    Task<string?> SecretGetirAsync(string jti);

    /// <summary>Web (SSR) istemci token'ı: sayfa render'ında HTML'e gömülür, site JS'i
    /// /api/* çağrılarında taşır. type=web — imza/secret gerektirmez (cihaz attestation'ı
    /// tarayıcıda mümkün değil; koruma katmanı: SSR'dan alma zorunluluğu + kısa ömür +
    /// sınırlı yenileme zinciri + rate limit). Turnstile eklenirse üretim bu noktada
    /// şartlanacak. yenilemeSayisi "rn" claim'ine yazılır — zincir sınırı renew ucunda.</summary>
    (string Token, DateTime ExpiresAt) WebTokenUret(int yenilemeSayisi = 0);
}

public sealed class DeviceTokenService(IConfiguration config, IDeviceStateStore stateStore) : IDeviceTokenService
{
    public async Task<DeviceTokenSonucu> TokenUretAsync(string platform)
    {
        var dakika = config.GetValue("MobileAttestation:DeviceTokenMinutes", 15);
        var jti = Guid.NewGuid().ToString("N");
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var bitis = DateTime.UtcNow.AddMinutes(dakika);

        var anahtar = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Secret"]!));
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Jti, jti),
                new Claim("type", "device"),
                new Claim("platform", platform),
            ],
            notBefore: DateTime.UtcNow,
            expires: bitis,
            signingCredentials: new SigningCredentials(anahtar, SecurityAlgorithms.HmacSha256));

        await stateStore.SecretKaydetAsync(jti, secret, bitis - DateTime.UtcNow);
        return new DeviceTokenSonucu(new JwtSecurityTokenHandler().WriteToken(token), secret, bitis);
    }

    public Task<string?> SecretGetirAsync(string jti) => stateStore.SecretGetirAsync(jti);

    public (string Token, DateTime ExpiresAt) WebTokenUret(int yenilemeSayisi = 0)
    {
        var dakika = config.GetValue("MobileAttestation:WebTokenMinutes", 15);
        var bitis = DateTime.UtcNow.AddMinutes(dakika);
        var anahtar = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Secret"]!));
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new Claim("type", "web"),
                new Claim("rn", yenilemeSayisi.ToString()),
            ],
            notBefore: DateTime.UtcNow,
            expires: bitis,
            signingCredentials: new SigningCredentials(anahtar, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), bitis);
    }
}
