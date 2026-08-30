namespace ECSPros.Shared.Contracts;

/// <summary>
/// FAZ 10 / A9 — süreç-içi (IMemoryCache) önbellek geçersizleştirmesinin düğümler arası
/// yayını. Admin komutları anahtar sildiğinde bu port kullanılır: anahtar önce YEREL
/// IMemoryCache'ten silinir, ardından Redis pub/sub (`ECSPros:cache:bust`) ile diğer
/// düğümlere duyurulur — her düğümün abonesi kendi belleğinden siler.
/// Hata-güvenli: Redis yoksa/erişilemezse yalnız yerel silme yapılır (bu cache'lerin
/// TTL'leri kısa — 60 sn/2 dk — olduğundan diğer düğümler en geç TTL sonunda tazelenir).
/// </summary>
public interface ICacheBustPublisher
{
    /// <summary>Anahtarı yerel bellekten siler ve tüm düğümlere yayınlar (fire-and-forget).</summary>
    void Bust(string anahtar);
}
