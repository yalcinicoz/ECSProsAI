namespace ECSPros.Shared.Contracts;

/// <summary>
/// FAZ 10 / A4 — hesap bazlı hatalı giriş deneme sayacı (brute-force freni).
/// Çoklu düğümde sayaç Redis'te tutulur ki kilit tüm düğümlerde geçerli olsun;
/// Redis erişilemezse uygulama düğüm-yerel sayaca düşer (giriş hiçbir zaman
/// "sayaç yok" diye bloke edilmez — attestation'ın aksine fail-open).
/// Anahtar çağıran tarafça verilir (ör. "uye-giris-hata:{eposta}").
/// </summary>
public interface ILoginAttemptCounter
{
    /// <summary>Sayacı 1 artırır ve güncel değeri döner; her artışta pencere süresi yenilenir
    /// (mevcut davranış: kilit eşiğinin ALTINDAYKEN her deneme süreyi uzatır).</summary>
    Task<int> ArtirAsync(string anahtar, TimeSpan pencere, CancellationToken ct = default);

    /// <summary>Güncel sayacı döner (yoksa 0).</summary>
    Task<int> GetirAsync(string anahtar, CancellationToken ct = default);

    /// <summary>Başarılı girişte sayacı sıfırlar.</summary>
    Task SifirlaAsync(string anahtar, CancellationToken ct = default);
}
