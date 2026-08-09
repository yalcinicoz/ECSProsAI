namespace ECSPros.Fulfillment.Application.Services;

/// <summary>
/// OP1: toplama görevi oluşturma için sipariş adayı okuma — Order/Inventory/Catalog
/// şemalarından proje referansı vermeden (OrderPackagingReader kalıbı, raw SQL).
/// Aday = Status=confirmed, henüz bir plana bağlanmamış (PickingPlanId null) sipariş.
/// </summary>
public interface IOrderPickingReader
{
    Task<List<PickingCandidate>> GetCandidatesAsync(PickingTaskFilter filter, CancellationToken ct = default);

    /// <summary>Seçili siparişlerin kalemleri (varyant barkoduyla) + rezervasyon rafları.</summary>
    Task<PickingLineSource> GetLineSourcesAsync(List<Guid> orderIds, CancellationToken ct = default);
}

public record PickingTaskFilter(
    List<Guid>? FirmPlatformIds = null,
    Guid? WarehouseId = null,          // siparişin TÜM rezervasyonları bu depodaysa
    int? MinItems = null,              // toplam adet
    int? MaxItems = null,
    Guid? CargoIntegrationId = null,
    Guid? ShippingCityId = null,
    DateTime? From = null,
    DateTime? To = null);

public record PickingCandidate(
    Guid OrderId,
    string OrderNumber,
    Guid FirmPlatformId,
    Guid ShippingCityId,
    Guid? CargoIntegrationId,
    string? CargoName,
    DateTime CreatedAt,
    int TotalQuantity,
    List<Guid> WarehouseIds)           // rezervasyonların depoları (boş = rezervasyonsuz)
{
    public bool TekUrunlu => TotalQuantity == 1;
    public bool KarmaDepolu => WarehouseIds.Count > 1;
}

public record PickingLineSource(
    List<PickingItemRow> Items,
    List<PickingReservationRow> Reservations);

public record PickingItemRow(
    Guid OrderItemId, Guid OrderId, Guid VariantId, int Quantity, string? Barcode,
    string? Sku, string? ProductName, string? VariantInfo);

public record PickingReservationRow(
    Guid OrderId, Guid VariantId, int Quantity,
    Guid? BinId, string? BinCode, int SectionOrder, int BinOrder);
