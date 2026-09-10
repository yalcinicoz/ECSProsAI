using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OrderEntity = ECSPros.Order.Domain.Entities.Order;

namespace ECSPros.Order.Application.Queries.GetOrders;

/// <summary>
/// Siparişler DataGrid şeması (plan F0 pilotu): beyaz listeli sıralama/filtre alanları + mevcut adlandırılmış filtrelerin
/// tek yerden uygulanması (liste, sekme sayaçları ve export AYNI filtre modelini kullanır → ekran/Excel tutarlı).
/// </summary>
public static class OrderGrid
{
    public static readonly string[] Statuses = { "pending", "confirmed", "processing", "shipped", "delivered", "cancelled", "returned" };
    public static readonly string[] PaymentStatuses = { "pending", "unpaid", "paid", "partial", "refunded", "failed" };
    public static readonly string[] PaymentMethods = { "kart", "kapida-nakit", "kapida-kart", "none" };

    public static readonly GridSchema<OrderEntity> Schema = new GridSchema<OrderEntity>()
        .Text("orderNumber", o => o.OrderNumber)
        .Text("externalOrderNumber", o => o.ExternalOrderNumber)
        .Text("customer", o => o.ShippingRecipientName)
        .Text("phone", o => o.ShippingRecipientPhone)
        .Text("cargo", o => o.RequestedCargoName)
        .Enum("status", o => o.Status, Statuses)
        .Enum("paymentStatus", o => o.PaymentStatus, PaymentStatuses)
        .Enum("paymentMethod", o => o.PaymentMethod, PaymentMethods, nullToken: "none")
        .Enum("orderType", o => o.OrderType)
        .Date("createdAt", o => o.CreatedAt)
        .Number("total", o => o.GrandTotal)
        .Bool("paid", o => o.PaymentStatus == "paid")
        .Guid("firmPlatformId", o => o.FirmPlatformId)
        .Kanal(o => o.FirmPlatformId)   // Y3: kanal kapsamı kolonu (K2)
        .Guid("memberId", o => o.MemberId)
        .Sort("createdAt", o => o.CreatedAt)
        .Sort("orderNumber", o => o.OrderNumber)
        .Sort("total", o => o.GrandTotal)
        .Sort("status", o => o.Status)
        .Sort("customer", o => o.ShippingRecipientName)
        .Sort("paymentStatus", o => o.PaymentStatus)
        .Sort("cargo", o => o.RequestedCargoName)   // FAZ 15.2c (2026-09-10): kargo kolonu
        .DefaultSort(o => o.CreatedAt, desc: true)
        .TieBreaker(o => o.Id);

    /// <summary>Mevcut adlandırılmış filtreler (eski istemciler/derin linkler bozulmaz) + global arama.</summary>
    public static IQueryable<OrderEntity> ApplyNamed(IQueryable<OrderEntity> query, OrderListFilters f, bool includeStatus = true)
    {
        if (includeStatus)
        {
            if (!string.IsNullOrWhiteSpace(f.Status)) query = query.Where(o => o.Status == f.Status);
            if (f.Statuses is { Count: > 0 }) query = query.Where(o => f.Statuses.Contains(o.Status));
        }
        if (f.MemberId.HasValue) query = query.Where(o => o.MemberId == f.MemberId);
        if (f.FirmPlatformId.HasValue) query = query.Where(o => o.FirmPlatformId == f.FirmPlatformId.Value);
        if (f.CreatedFrom.HasValue) query = query.Where(o => o.CreatedAt >= f.CreatedFrom.Value);
        if (f.CreatedTo.HasValue) query = query.Where(o => o.CreatedAt < f.CreatedTo.Value); // exclusive üst sınır
        // 2026-08-04: "none" = yöntemi kayıtlı olmayan (eski/başka kanal)
        if (!string.IsNullOrWhiteSpace(f.PaymentMethod))
            query = f.PaymentMethod == "none" ? query.Where(o => o.PaymentMethod == null) : query.Where(o => o.PaymentMethod == f.PaymentMethod);
        // 2026-08-04: alınan = paid; alınmayan = paid dışı her durum
        if (f.PaymentCollected.HasValue)
            query = f.PaymentCollected.Value ? query.Where(o => o.PaymentStatus == "paid") : query.Where(o => o.PaymentStatus != "paid");
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            // Global arama (plan §2.3): sipariş no, dış sipariş no, alıcı adı, telefon.
            var term = f.Search.Trim().ToLower();
            query = query.Where(o =>
                o.OrderNumber.ToLower().Contains(term) ||
                o.ShippingRecipientName.ToLower().Contains(term) ||
                o.ShippingRecipientPhone.Contains(term) ||
                (o.ExternalOrderNumber != null && o.ExternalOrderNumber.ToLower().Contains(term)));
        }
        return query;
    }

    /// <summary>Adlandırılmış + grid filtreleri (grid'de status hariç tutulabilir → sekme sayaçları).</summary>
    /// <summary>FAZ 15.2c (2026-09-10, eski "Üründen Sipariş Sorgula"): şemaya girmeyen kalem filtresi — <c>f.product</c>
    /// sipariş kalemlerinde ürün kodu (Sku) ya da ürün adında geçen siparişleri getirir (koleksiyon; GridSchema tek
    /// alanlı ifade istediği için handler'da özel uygulanır, ApplyFilters'ta atlanır).</summary>
    public static IQueryable<OrderEntity> ApplyProductFilter(IQueryable<OrderEntity> query, GridRequest? grid)
    {
        var filter = grid?.Filters.FirstOrDefault(x => string.Equals(x.Field, "product", StringComparison.OrdinalIgnoreCase));
        if (filter is null || string.IsNullOrWhiteSpace(filter.Value)) return query;
        var term = filter.Value.Trim().ToLower();
        return filter.Op switch
        {
            "eq" => query.Where(o => o.Items.Any(i => i.Sku.ToLower() == term)),
            "startswith" => query.Where(o => o.Items.Any(i => i.Sku.ToLower().StartsWith(term) || i.ProductName.ToLower().StartsWith(term))),
            _ => query.Where(o => o.Items.Any(i => i.Sku.ToLower().Contains(term) || i.ProductName.ToLower().Contains(term))),
        };
    }

    public static IQueryable<OrderEntity> ApplyAll(IQueryable<OrderEntity> query, OrderListFilters f, GridRequest? grid, bool includeStatus = true)
    {
        query = ApplyNamed(query, f, includeStatus);
        query = ApplyProductFilter(query, grid);
        query = includeStatus ? Schema.ApplyFilters(query, grid, "product") : Schema.ApplyFilters(query, grid, "status", "product");
        // Y3 (K2): kullanıcının erişemediği kanalın siparişi listeye/sayıma/exporta girmez.
        return Schema.ApplyKanalKapsami(query, grid?.KanalKisiti);
    }

    // ── Excel etiketleri (panel orderConstants ile aynı) ──
    // M4 (2026-09-09): metinler DurumEtiketleri.Panel'de tek yerde; store/mobil uçları
    // DurumEtiketleri.Vitrin'i kullanır (müşteri dili — bilinçli farklı).
    public static string StatusLabel(string s)
        => DurumEtiketleri.Etiket(DurumEtiketleri.Panel.SiparisDurumu, s);
    public static string PaymentStatusLabel(string s)
        => DurumEtiketleri.Etiket(DurumEtiketleri.Panel.OdemeDurumu, s);
    public static string PaymentMethodLabel(string? s)
        => DurumEtiketleri.Etiket(DurumEtiketleri.Panel.OdemeYontemi, s);
}

/// <summary>Sipariş listesinin adlandırılmış filtreleri (GET parametreleri / export gövdesi "named").</summary>
public record OrderListFilters(
    string? Status = null, List<string>? Statuses = null, Guid? MemberId = null, Guid? FirmPlatformId = null,
    DateTime? CreatedFrom = null, DateTime? CreatedTo = null, string? PaymentMethod = null, bool? PaymentCollected = null,
    string? Search = null);

/// <summary>Excel satırı — export kolon tanımı (Api) bu alanlardan seçer.</summary>
public record OrderExportRow(
    string OrderNumber, string? ExternalOrderNumber, DateTime CreatedAt, string Status, string PaymentStatus, string? PaymentMethod,
    string RecipientName, string RecipientPhone, string AddressLine, string? PostalCode, string? RequestedCargoName, string OrderType,
    decimal Subtotal, decimal TotalDiscount, decimal TotalExpense, decimal TotalTax, decimal GrandTotal, string CurrencyCode);

/// <summary>Export kaynağı: sayfalamasız, sıralı sorgu + toplam (tavan kontrolü için). Sorgu çağıranın scope'unda tüketilir.</summary>
public record OrderExportSource(int Count, IQueryable<OrderExportRow> Rows);

public record ExportOrdersQuery(OrderListFilters Filters, GridRequest Grid, int MaxRows) : IRequest<Result<OrderExportSource>>;

public class ExportOrdersQueryHandler(IOrderDbContext db) : IRequestHandler<ExportOrdersQuery, Result<OrderExportSource>>
{
    public async Task<Result<OrderExportSource>> Handle(ExportOrdersQuery r, CancellationToken ct)
    {
        var q = OrderGrid.ApplyAll(db.Orders.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<OrderExportSource>($"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = OrderGrid.Schema.ApplySort(q, r.Grid).Select(o => new OrderExportRow(
            o.OrderNumber, o.ExternalOrderNumber, o.CreatedAt, o.Status, o.PaymentStatus, o.PaymentMethod,
            o.ShippingRecipientName, o.ShippingRecipientPhone, o.ShippingAddressLine, o.ShippingPostalCode, o.RequestedCargoName, o.OrderType,
            o.Subtotal, o.TotalDiscount, o.TotalExpense, o.TotalTax, o.GrandTotal, o.CurrencyCode));
        return Result.Success(new OrderExportSource(count, rows));
    }
}
