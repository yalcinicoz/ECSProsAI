using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>E6: koleksiyon ürünü — ProductCode anahtarlı (E5 favori kararıyla aynı;
/// listelemede canlı katalogla birleşir, silinen ürün görünmez).</summary>
public class CollectionItem : BaseEntity
{
    public Guid CollectionId { get; set; }
    public string ProductCode { get; set; } = string.Empty;

    public Collection Collection { get; set; } = null!;
}
