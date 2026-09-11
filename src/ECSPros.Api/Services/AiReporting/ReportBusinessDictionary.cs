using ECSPros.Order.Domain.Entities;
using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Authorization;
using OrderEntity = ECSPros.Order.Domain.Entities.Order;

namespace ECSPros.Api.Services.AiReporting;

/// <summary>Business semantics, not canned reports. No credentials, SQL names or customer records.</summary>
public static class ReportBusinessDictionary
{
    public static ReportEntityDefinition<OrderEntity> Orders { get; } = new ReportEntityDefinition<OrderEntity>(Permissions.OrdersView)
        .Text("orders.status", "Sipariş durumu", o => o.Status)
        .Text("orders.paymentStatus", "Sipariş ödeme durumu", o => o.PaymentStatus, filterable: false)
        .Text("orders.paymentMethod", "Siparişte seçilen ödeme yöntemi (gerçek tahsilat değildir)", o => o.PaymentMethod, filterable: false)
        .Text("orders.orderType", "Sipariş tipi", o => o.OrderType, filterable: false)
        .Text("orders.currencyCode", "Para birimi", o => o.CurrencyCode)
        .Count("orders.count", "Sipariş sayısı")
        .Measure("orders.amount", "Sipariş tutarı (GrandTotal; tahsilat/net satış değildir)", o => o.GrandTotal, "sum", "orders.currencyCode", filterable: true)
        .Measure("orders.averageAmount", "Ortalama sipariş tutarı", o => o.GrandTotal, "average", "orders.currencyCode")
        .Measure("orders.minimumAmount", "En düşük sipariş tutarı", o => o.GrandTotal, "minimum", "orders.currencyCode")
        .Measure("orders.maximumAmount", "En yüksek sipariş tutarı", o => o.GrandTotal, "maximum", "orders.currencyCode")
        .Text("orders.orderNumber", "Sipariş no", o => o.OrderNumber, groupable: false)
        .Text("orders.memberId", "Kayıtlı müşteri kimliği (misafir siparişinde boş)", o => o.MemberId.HasValue ? o.MemberId.Value.ToString() : null)
        .RequirePermission("orders.memberId", Permissions.CrmMembersView)
        .DistinctCount("orders.memberCount", "Tekil kayıtlı müşteri sayısı (misafirler hariç)", o => o.MemberId)
        .RequirePermission("orders.memberCount", Permissions.CrmMembersView)
        .Value("orders.createdAt", "Sipariş tarihi", o => o.CreatedAt).Seal();

    public static ReportEntityDefinition<OrderPayment> Payments { get; } = new ReportEntityDefinition<OrderPayment>(Permissions.OrdersView)
        .Text("payments.status", "Ödeme kaydı durumu", p => p.Status, groupable: false)
        .Value("payments.methodId", "Ödeme yöntemi kimliği", p => p.PaymentMethodId)
        .Value("payments.amount", "Ödeme kaydı tutarı", p => p.Amount)
        .Text("payments.currencyCode", "Ödeme kaydı para birimi", p => p.CurrencyCode, groupable: false)
        .Value("payments.createdAt", "Ödeme kaydı oluşturulma tarihi", p => p.CreatedAt).Seal();

    public static ReportEntityDefinition<Return> Returns { get; } = new ReportEntityDefinition<Return>(Permissions.OrdersReturnsView)
        .Text("returns.status", "İade kaydı durumu", r => r.Status, groupable: false)
        .Text("returns.type", "İade kaydı tipi", r => r.ReturnType, groupable: false)
        .Value("returns.createdAt", "İade kaydı oluşturulma tarihi", r => r.CreatedAt).Seal();

    public static ReportRelationDefinition<OrderEntity, OrderPayment, Guid> OrderPayments { get; } =
        new("orders.payments", "Bağlı ödeme kayıtları", Payments, o => o.Id, p => p.OrderId);
    // The projection reads current catalog data: match the existing product-filter authorization boundary.
    public static ReportEntityDefinition<OrderReportLine> Items { get; } = new ReportEntityDefinition<OrderReportLine>(Permissions.CatalogProductsView)
        .Text("items.sku", "Sipariş satırındaki SKU (snapshot; ürün kodu/barkod değildir)", i => i.Sku, groupable: false)
        .Text("items.productName", "Sipariş anındaki ürün adı", i => i.ProductName, groupable: false)
        .Text("items.productCode", "Güncel aktif katalog ürün kodu; SKU fallback yok", i => i.ProductCode, groupable: false)
        .Text("items.barcode", "Güncel aktif varyant barkodu; SKU fallback yok", i => i.Barcode, groupable: false)
        .Value("items.quantity", "Tek sipariş satırındaki adet (satırlar toplamı değil)", i => i.Quantity).Seal();
    public static ReportRelationDefinition<OrderEntity, OrderReportLine, Guid> OrderItems { get; } =
        new("orders.items", "Siparişin ürün satırları (aynı satır koşulu)", Items, o => o.Id, i => i.OrderId);
    public static ReportRelationDefinition<OrderEntity, Return, Guid> OrderReturns { get; } =
        new("orders.returns", "Yetki kapsamındaki bağlı iade kayıtları", Returns, o => o.Id, r => r.OrderId);
}
