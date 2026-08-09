namespace ECSPros.Shared.Contracts;

/// <summary>
/// Ürün Kartı F2 (2026-08-09): kartın değişken alanlarında (1 = görsel altı bant,
/// 2 = ürün adı altı satır, 3 = puan altı satır) dönecek elle tanımlı mesajlar.
/// Color: palet anahtarı (yesil|turuncu|bordo|pembe) — alan 2/3'te metin rengi sınıfı,
/// alan 1'de bant arka plan rengine çevrilir. Icon: Font Awesome sınıf adı (fa-truck gibi).
/// </summary>
public record CardMessageItem(int Slot, string Text, string? Icon, string? Color);

/// <summary>
/// Kanalın aktif kart mesajlarını ürün başına çözer (kapsam: tüm ürünler / kanal
/// kategorileri / ürün kodu listesi; tarih penceresi uygulanır). Kampanya resolver'ı
/// gibi cache SONRASI çağrılır — mesaj değişikliği TTL beklemeden yansır.
/// Implementasyon Storefront modülünde (card_messages + channel kategori üyeliği orada).
/// </summary>
public interface ICardMessageResolver
{
    /// <param name="productCodesById">sayfadaki ürünler: ProductId → ürün kodu</param>
    /// <returns>ProductId → slot/sıra düzeninde mesaj listesi (mesajı olmayan ürün yer almaz)</returns>
    Task<Dictionary<Guid, List<CardMessageItem>>> ResolveForProductsAsync(
        Guid firmPlatformId,
        IReadOnlyDictionary<Guid, string> productCodesById,
        CancellationToken ct = default);
}
