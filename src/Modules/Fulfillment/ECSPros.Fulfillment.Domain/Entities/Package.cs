using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Fulfillment.Domain.Entities;

public class Package : BaseEntity
{
    public Guid OrderId { get; set; }
    /// <summary>Siparişin satış kanalı (denormalize) — paket no kanal serisinden
    /// üretilir ve (FirmPlatformId, PackageNumber) unique'tir (F2, 2026-07-19).</summary>
    public Guid FirmPlatformId { get; set; }
    public Guid? ShipmentId { get; set; }
    /// <summary>Kanala özel seriden üretilen bağımsız paket numarası (örn. MISP000042).
    /// Sipariş numarasıyla kaynaştırılmaz; değişirse eski değer kod geçmişine yazılır.</summary>
    public string PackageNumber { get; set; } = string.Empty;
    /// <summary>Sipariş içi görsel sıra (1,2,3…) — kimlik değildir.</summary>
    public int SequenceInOrder { get; set; }
    /// <summary>Paketin tedarikçisi — tedarikçi bazlı bölmede atanır; karma pakette null.</summary>
    public Guid? SupplierId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    /// <summary>Kargo entegrasyon kodu — üretim kuralı kargo firmasına özeldir (F3);
    /// pazaryeri kodu geldiyse aynen yazılır.</summary>
    public string? CargoIntegrationCode { get; set; }
    /// <summary>generated: kurala göre bizde üretildi; external: pazaryeri/taşıyıcı verdi.</summary>
    public string? CargoIntegrationCodeSource { get; set; }
    public decimal? Weight { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public decimal? Length { get; set; }
    public decimal? Desi { get; set; }
    /// <summary>Satıcı paneli (2026-08-11): satıcının kendi kestiği faturanın numarası/linki —
    /// paket başına fatura kuralıyla uyumlu; relay e-posta (P3b) gelene dek görüntü linki serbest.</summary>
    public string? SupplierInvoiceNumber { get; set; }
    public string? SupplierInvoiceUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? PackedAt { get; set; }
    public Guid? PackedBy { get; set; }
    public DateTime? LabelPrintedAt { get; set; }

    public ICollection<PackageItem> Items { get; set; } = new List<PackageItem>();
}
