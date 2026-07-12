using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

public class ProductVideo : BaseEntity
{
    public Guid ProductId { get; set; }
    public Guid ImageSetId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? ThumbnailFileName { get; set; }
    public int SortOrder { get; set; } = 0;
    public ProductImageStatus Status { get; set; } = ProductImageStatus.Pending;
    public Guid BatchId { get; set; }
    public DateTime? ArchivedAt { get; set; }

    // H5 (K15): URL tabanlı video — kullanıcının video sunucusundaki ya da dış kaynaktaki
    // adres. Doluysa efektif kaynak budur; FileName/FTP yükleme akışı olduğu gibi durur,
    // iki yol bir arada yaşar (URL kayıtlarında FileName boş kalır).
    public string? VideoUrl { get; set; }
    public string? ThumbnailUrl { get; set; }

    public Product Product { get; set; } = null!;
    public ImageSet ImageSet { get; set; } = null!;
}
