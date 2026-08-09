namespace ECSPros.Fulfillment.Application.Services;

/// <summary>Paketleme için sipariş bilgisi okuma — Order modülüne proje referansı
/// vermeden (modüller arası gevşek bağ) sipariş başlığı + kalemleri döner.</summary>
public interface IOrderPackagingReader
{
    Task<OrderPackagingInfo?> GetOrderAsync(Guid orderId, CancellationToken ct = default);
}

public record OrderPackagingInfo(
    Guid OrderId,
    Guid FirmPlatformId,
    string OrderNumber,
    string Status,
    List<OrderPackagingItem> Items,
    // OP2: etiket basımı için alıcı/kargo bilgisi
    string? RecipientName = null,
    string? RecipientPhone = null,
    string? AddressLine = null,
    string? CargoName = null);

public record OrderPackagingItem(
    Guid OrderItemId,
    Guid VariantId,
    Guid? SupplierId,
    int Quantity);
