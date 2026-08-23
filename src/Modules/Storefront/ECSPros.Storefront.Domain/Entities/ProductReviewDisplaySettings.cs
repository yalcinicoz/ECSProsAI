using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// Platform bazlı çok kaynaklı puan/yorum GÖRÜNÜM ayarı — kalıcı, pazaryeri agnostik.
/// Hangi kanalların ortalamaya (toplam puana) katılacağını, hangi kanalların yorumlarının
/// ürün sayfasında listeleneceğini ve yorum görsellerinin gösterilip gösterilmeyeceğini
/// belirler. Platform başına tek satır; satır yoksa varsayılanlar uygulanır:
/// toplamaya tüm kanallar, listelemeye own+trendyol, görseller açık.
/// </summary>
public class ProductReviewDisplaySettings : BaseEntity
{
    public Guid FirmPlatformId { get; set; }

    /// <summary>Toplam puana katılan kanallar (kod listesi; boş = tümü).</summary>
    public List<string> AggregateChannels { get; set; } = new();

    /// <summary>Ürün sayfasında yorumları listelenecek kanallar (kod listesi; boş = tümü).</summary>
    public List<string> ListChannels { get; set; } = new();

    /// <summary>Yorum görselleri gösterilsin mi.</summary>
    public bool ShowReviewPhotos { get; set; } = true;
}
