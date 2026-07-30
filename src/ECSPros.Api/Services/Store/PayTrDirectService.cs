using System.Security.Cryptography;
using System.Text;

namespace ECSPros.Api.Services.Store;

/// <summary>
/// PayTR Direct API entegrasyonu (2026-07-30). Yalnız TEST MODU — canlı için PCI-DSS SAQ D
/// uyumu + PayTR Direct API onayı gerekir (bkz. docs/paytr-entegrasyon-plani.md).
///
/// ★ KART VERİSİ GÜVENLİK KURALLARI (ihlali doğrudan PCI ihlalidir):
///   - Tam kart numarası ve CVV ASLA loglanmaz, DB'ye/diske yazılmaz, cache'lenmez.
///     Bu sınıf kartı yalnız PayTR'a POST etmek için bellekte tutar, sonra bırakır.
///   - Yalnız MASKELİ PAN (ilk 6 + son 4, ortası •) hukuki/itiraz amacıyla saklanabilir.
///   - Hiçbir exception mesajına/log satırına kart alanı KONMAZ.
///   Bu kuralları zayıflatan değişiklik yapılmamalıdır.
/// </summary>
public class PayTrDirectService(
    IHttpClientFactory httpClientFactory,
    ILogger<PayTrDirectService> logger)
{
    private const string OdemeUrl = "https://www.paytr.com/odeme";

    /// <summary>Kart numarasını PCI-uyumlu maskeler: ilk 6 + son 4 görünür, ortası gizli
    /// (ör. "454671••••••1234"). Girdi bellekten çağıran tarafından hemen bırakılmalı.</summary>
    public static string MaskePan(string kartNo)
    {
        var rakam = new string(kartNo.Where(char.IsDigit).ToArray());
        if (rakam.Length < 10) return "••••"; // güvenli taban — asla ham değeri döndürme
        var ilk6 = rakam[..6];
        var son4 = rakam[^4..];
        var orta = new string('•', rakam.Length - 10);
        return ilk6 + orta + son4;
    }

    /// <summary>Adım 1 token: base64(HMAC-SHA256(
    ///   merchant_id+user_ip+merchant_oid+email+payment_amount+payment_type+
    ///   installment_count+currency+test_mode+non_3d + merchant_salt, merchant_key)).
    /// PayTR Direct API 1. adım dokümanındaki sıra birebir.</summary>
    public static string Adim1Token(
        string merchantId, string userIp, string merchantOid, string email,
        string paymentAmount, string paymentType, string installmentCount,
        string currency, string testMode, string non3d,
        string merchantKey, string merchantSalt)
    {
        var hashStr = merchantId + userIp + merchantOid + email + paymentAmount
            + paymentType + installmentCount + currency + testMode + non3d + merchantSalt;
        return HmacBase64(hashStr, merchantKey);
    }

    /// <summary>Callback doğrulama: gelen hash, base64(HMAC-SHA256(
    ///   merchant_oid + merchant_salt + status + total_amount, merchant_key)) ile eşleşmeli.
    /// Sabit-zamanlı karşılaştırma (zamanlama saldırısına kapalı).</summary>
    public static bool CallbackHashGecerli(
        string merchantOid, string status, string totalAmount,
        string gelenHash, string merchantKey, string merchantSalt)
    {
        var beklenen = HmacBase64(merchantOid + merchantSalt + status + totalAmount, merchantKey);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(beklenen), Encoding.UTF8.GetBytes(gelenHash ?? ""));
    }

    private static string HmacBase64(string veri, string anahtar)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(anahtar));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(veri)));
    }

    /// <summary>PayTR sepet formatı: [["ürün adı","birim fiyat (TL string)",adet], ...]
    /// base64(JSON). Fiyatlar TL cinsinden string (PayTR örneği böyle).</summary>
    public static string SepetBase64(IEnumerable<(string Ad, decimal BirimFiyat, int Adet)> kalemler)
    {
        var dizi = kalemler.Select(k => new object[]
        {
            k.Ad.Length > 100 ? k.Ad[..100] : k.Ad,
            k.BirimFiyat.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            k.Adet
        });
        var json = System.Text.Json.JsonSerializer.Serialize(dizi);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    /// <summary>PayTR /odeme'ye POST eder ve dönen içeriği aynen verir. 3D akışında PayTR
    /// bir HTML sayfası döner (tarayıcıya basılıp bankanın 3D sayfasına yönlenir); non_3d'de
    /// JSON döner. Çağıran içeriği tarayıcıya iletir. Kart alanları burada yalnız istek
    /// gövdesindedir — YANIT loglanırken kart verisi zaten yoktur, yine de gövde loglanmaz.</summary>
    public async Task<PayTrOdemeSonucu> OdemeBaslatAsync(
        IReadOnlyDictionary<string, string> formAlanlari, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("paytr");
        client.Timeout = TimeSpan.FromSeconds(20);
        try
        {
            using var icerik = new FormUrlEncodedContent(formAlanlari);
            using var yanit = await client.PostAsync(OdemeUrl, icerik, ct);
            var govde = await yanit.Content.ReadAsStringAsync(ct);
            // NOT: govde loglanmaz — 3D HTML/JSON, kart verisi taşımaz ama ihtiyat.
            return new PayTrOdemeSonucu(yanit.IsSuccessStatusCode, (int)yanit.StatusCode, govde);
        }
        catch (Exception ex)
        {
            // Mesaja form alanları KONMAZ (kart verisi sızmasın).
            logger.LogError(ex, "PayTR /odeme çağrısı başarısız (merchant_oid gövdede, loglanmadı).");
            return new PayTrOdemeSonucu(false, 0, null);
        }
    }
}

public record PayTrOdemeSonucu(bool Basarili, int DurumKodu, string? Icerik);
