namespace ECSPros.Inventory.Domain.Entities;

public class StockMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VariantId { get; set; }
    public Guid? FromWarehouseId { get; set; }
    public Guid? ToWarehouseId { get; set; }
    public Guid? FromLocationId { get; set; }
    public Guid? ToLocationId { get; set; }
    // T5 (tedarik): hareketler artık BİRİM (bin) düzeyinde izlenebilir — stok bin bazlı, hareket depo bazlıydı
    // (izlenebilirlik kırığı). Eski kayıtlar null kalır; yeni yerleştirme/bin hareketleri doldurur.
    public Guid? FromBinId { get; set; }
    public Guid? ToBinId { get; set; }
    public string MovementType { get; set; } = string.Empty; // purchase, sale, return, transfer, adjustment, defective, donation
    public int Quantity { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
}
