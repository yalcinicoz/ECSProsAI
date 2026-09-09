using ECSPros.Order.Application.Queries.GetOrders;

namespace ECSPros.Api.Grid;

/// <summary>Siparişler Excel kolonları (anahtarlar panel DataGrid kolon anahtarlarıyla aynı; sipariş no + durum kilitli).</summary>
public static class OrderExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<OrderExportRow>> All = new GridExportColumn<OrderExportRow>[]
    {
        new("orderNumber", "Sipariş No", r => r.OrderNumber, Locked: true),
        new("createdAt", "Tarih", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
        new("status", "Durum", r => OrderGrid.StatusLabel(r.Status), Locked: true),
        new("paymentStatus", "Ödeme Durumu", r => OrderGrid.PaymentStatusLabel(r.PaymentStatus)),
        new("paymentMethod", "Ödeme Yöntemi", r => OrderGrid.PaymentMethodLabel(r.PaymentMethod)),
        new("customer", "Müşteri", r => r.RecipientName),
        new("phone", "Telefon", r => r.RecipientPhone, AlanYetkisi: "phone"),
        new("address", "Adres", r => r.AddressLine, AlanYetkisi: "address"),
        new("postalCode", "Posta Kodu", r => r.PostalCode),
        new("cargo", "Kargo", r => r.RequestedCargoName),
        new("orderType", "Sipariş Tipi", r => r.OrderType),
        new("subtotal", "Ara Toplam", r => r.Subtotal),
        new("totalDiscount", "İndirim", r => r.TotalDiscount),
        new("totalExpense", "Masraf", r => r.TotalExpense),
        new("totalTax", "KDV", r => r.TotalTax),
        new("total", "Genel Toplam", r => r.GrandTotal),
        new("currency", "Para Birimi", r => r.CurrencyCode),
        new("externalOrderNumber", "Dış Sipariş No", r => r.ExternalOrderNumber),
    };
}
