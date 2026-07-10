using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// E12: üyenin gezdiği ürün (Önceden Gezdiklerim). Ürün başına TEK kayıt tutulur —
/// tekrar gezmede ViewedAt güncellenir (upsert); üye başına son 50 kayıt saklanır,
/// fazlası kayıt anında budanır. Misafir gezmeleri DB'ye yazılmaz — localStorage
/// fallback'i detay sayfası script'indedir (Faz G "son gezilenler" bloğu iki kaynağı
/// da kullanır). Anahtar ProductCode (E5 Favorite deseni).
/// </summary>
public class ViewedProduct : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public Guid MemberId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public DateTime ViewedAt { get; set; }
}
