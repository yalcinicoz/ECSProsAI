using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetSupplierOrders;

/// <summary>
/// Partner P1b (2026-08-11): satıcıya (SupplierId) düşen siparişler — owner-scoped.
/// K2 kararı: müşteriden yalnız ad-soyad + teslimat adresi paylaşılır (telefon, e-posta,
/// demografi ASLA; relay e-posta P3b'de gelecek). Kalemler yalnız satıcının kalemleridir;
/// sipariş toplamları/diğer satıcı kalemleri dönmez. 'pending' siparişler görünmez
/// (onaylanmamış satış satıcıya bildirilmez); Since polling içindir (K6 v1) ve
/// COALESCE(UpdatedAt, CreatedAt) üzerinden çalışır — durum değişimleri de yakalanır.
/// </summary>
public record GetSupplierOrdersQuery(
    Guid SupplierId,
    DateTime? Since = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<SupplierOrderDto>>>;

public record GetSupplierOrderDetailQuery(Guid SupplierId, string OrderNumber)
    : IRequest<Result<SupplierOrderDto>>;

/// <summary>K2 alan kısıtlı sipariş görünümü — telefon/e-posta/demografi YOK.</summary>
public record SupplierOrderDto(
    string OrderNumber,
    string Status,
    string PaymentStatus,
    string CurrencyCode,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    SupplierOrderShippingDto Shipping,
    List<SupplierOrderItemDto> Items)
{
    /// <summary>Kompozisyon için iç kimlik — API yanıtına serileştirilmez (host atar).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Guid OrderId { get; init; }
}

public record SupplierOrderShippingDto(
    string RecipientName,
    string AddressLine,
    Guid CityId,
    Guid DistrictId,
    string? PostalCode,
    string? DeliveryNotes)
{
    /// <summary>Şehir/ilçe adları host'ta CRM geo tablolarından doldurulur.</summary>
    public string? CityName { get; set; }
    public string? DistrictName { get; set; }
}

public record SupplierOrderItemDto(
    Guid OrderItemId,
    string Sku,
    string ProductName,
    string VariantInfo,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal Total,
    string Status);

public class GetSupplierOrdersQueryHandler
    : IRequestHandler<GetSupplierOrdersQuery, Result<PagedResult<SupplierOrderDto>>>,
      IRequestHandler<GetSupplierOrderDetailQuery, Result<SupplierOrderDto>>
{
    private readonly IOrderDbContext _db;
    public GetSupplierOrdersQueryHandler(IOrderDbContext db) => _db = db;

    public async Task<Result<PagedResult<SupplierOrderDto>>> Handle(GetSupplierOrdersQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var q = _db.Orders.AsNoTracking()
            .Where(o => o.Status != "pending"
                && o.Items.Any(i => i.SupplierId == request.SupplierId && !i.IsDeleted));

        if (!string.IsNullOrWhiteSpace(request.Status))
            q = q.Where(o => o.Status == request.Status);

        if (request.Since is { } since)
            q = q.Where(o => (o.UpdatedAt ?? o.CreatedAt) >= since);

        var total = await q.CountAsync(ct);
        var orders = await q
            .OrderByDescending(o => o.UpdatedAt ?? o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(o => o.Items.Where(i => i.SupplierId == request.SupplierId && !i.IsDeleted))
            .ToListAsync(ct);

        var items = orders.Select(Donustur).ToList();
        return Result.Success(new PagedResult<SupplierOrderDto>(items, total, page, pageSize));
    }

    public async Task<Result<SupplierOrderDto>> Handle(GetSupplierOrderDetailQuery request, CancellationToken ct)
    {
        var order = await _db.Orders.AsNoTracking()
            .Include(o => o.Items.Where(i => i.SupplierId == request.SupplierId && !i.IsDeleted))
            .FirstOrDefaultAsync(o => o.OrderNumber == request.OrderNumber
                && o.Status != "pending"
                && o.Items.Any(i => i.SupplierId == request.SupplierId && !i.IsDeleted), ct);

        if (order is null)
            return Result.Failure<SupplierOrderDto>(
                $"'{request.OrderNumber}' numaralı, size ait kalem içeren bir sipariş bulunamadı.");

        return Result.Success(Donustur(order));
    }

    private static SupplierOrderDto Donustur(Domain.Entities.Order o) => new(
        o.OrderNumber, o.Status, o.PaymentStatus, o.CurrencyCode, o.CreatedAt, o.UpdatedAt,
        new SupplierOrderShippingDto(
            o.ShippingRecipientName, o.ShippingAddressLine,
            o.ShippingCityId, o.ShippingDistrictId,
            o.ShippingPostalCode, o.ShippingDeliveryNotes),
        o.Items.Select(i => new SupplierOrderItemDto(
            i.Id, i.Sku, i.ProductName, i.VariantInfo,
            i.Quantity, i.UnitPrice, i.DiscountAmount, i.Total, i.Status)).ToList())
    { OrderId = o.Id };
}
