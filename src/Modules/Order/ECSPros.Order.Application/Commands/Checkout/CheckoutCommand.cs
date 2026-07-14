using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using OrderEntity = ECSPros.Order.Domain.Entities.Order;
using OrderItemEntity = ECSPros.Order.Domain.Entities.OrderItem;

namespace ECSPros.Order.Application.Commands.Checkout;

public record CheckoutCommand(
    Guid FirmPlatformId,
    Guid MemberId,
    string CurrencyCode,
    // Shipping
    string ShippingRecipientName,
    string ShippingRecipientPhone,
    Guid ShippingCountryId,
    Guid ShippingCityId,
    Guid ShippingDistrictId,
    string ShippingAddressLine,
    string? ShippingPostalCode,
    string? ShippingDeliveryNotes,
    // Billing
    bool BillingSameAsShipping,
    string? BillingRecipientName,
    string? BillingTaxOffice,
    string? BillingTaxNumber,
    string? BillingCompanyName,
    Guid? BillingCountryId,
    Guid? BillingCityId,
    Guid? BillingDistrictId,
    string? BillingAddressLine,
    // Items
    List<CheckoutItem> Items,
    // Optional
    string? CustomerNotes = null,
    Guid? CartId = null,
    List<AcceptedContract>? AcceptedContracts = null) : IRequest<Result<Guid>>;

public record CheckoutItem(
    Guid VariantId,
    string Sku,
    string ProductName,
    string VariantInfo,
    int Quantity,
    decimal UnitPrice);

/// <summary>C8: sipariş anında onaylanan sözleşmelerin kaydı — Order.CustomerNotes
/// jsonb'sine "acceptedContracts" anahtarıyla yazılır. ContentUpdatedAt, onay anında
/// geçerli metnin sürümünü sabitler (CMS'te metin sonradan değişse de kanıt kalır).
/// JsonPropertyName: jsonb'deki diğer anahtarlarla (note) tutarlı camelCase için.</summary>
public record AcceptedContract(
    [property: System.Text.Json.Serialization.JsonPropertyName("code")] string Code,
    [property: System.Text.Json.Serialization.JsonPropertyName("title")] string Title,
    [property: System.Text.Json.Serialization.JsonPropertyName("acceptedAt")] DateTime AcceptedAt,
    [property: System.Text.Json.Serialization.JsonPropertyName("contentUpdatedAt")] DateTime? ContentUpdatedAt);

public class CheckoutCommandHandler(IOrderDbContext db, ECSPros.Shared.Contracts.IProductService productService)
    : IRequestHandler<CheckoutCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CheckoutCommand request, CancellationToken ct)
    {
        if (!request.Items.Any())
            return Result.Failure<Guid>("Sepet boş.");

        // Katman 1 (global satış anahtarı): satışa kapalı ürün SATILAMAZ — kanal ayarları ne
        // olursa olsun. ProductInfo.IsActive = varyant aktif VE ürün IsSaleOpen. Kapalı ürün
        // sepette kalmışsa (kapatılmadan önce eklenmiş) sipariş oluşturulmaz.
        foreach (var item in request.Items)
        {
            var bilgi = await productService.GetVariantAsync(item.VariantId, ct);
            if (bilgi is null || !bilgi.IsActive)
                return Result.Failure<Guid>($"'{item.ProductName}' şu an satışa kapalı; siparişi tamamlamak için sepetten çıkarın.");
        }

        var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

        var subtotal = request.Items.Sum(i => i.Quantity * i.UnitPrice);

        var order = new OrderEntity
        {
            OrderNumber = orderNumber,
            FirmPlatformId = request.FirmPlatformId,
            MemberId = request.MemberId,
            CartId = request.CartId,
            Status = "pending",
            PaymentStatus = "unpaid",
            OrderType = "retail",
            CurrencyCode = request.CurrencyCode,
            InvoiceCurrencyCode = request.CurrencyCode,
            ExchangeRate = 1m,
            ShippingRecipientName = request.ShippingRecipientName,
            ShippingRecipientPhone = request.ShippingRecipientPhone,
            ShippingCountryId = request.ShippingCountryId,
            ShippingCityId = request.ShippingCityId,
            ShippingDistrictId = request.ShippingDistrictId,
            ShippingAddressLine = request.ShippingAddressLine,
            ShippingPostalCode = request.ShippingPostalCode,
            ShippingDeliveryNotes = request.ShippingDeliveryNotes,
            BillingSameAsShipping = request.BillingSameAsShipping,
            BillingRecipientName = request.BillingRecipientName,
            BillingTaxOffice = request.BillingTaxOffice,
            BillingTaxNumber = request.BillingTaxNumber,
            BillingCompanyName = request.BillingCompanyName,
            BillingCountryId = request.BillingCountryId,
            BillingCityId = request.BillingCityId,
            BillingDistrictId = request.BillingDistrictId,
            BillingAddressLine = request.BillingAddressLine,
            Subtotal = subtotal,
            TotalDiscount = 0,
            TotalExpense = 0,
            TotalTax = 0,
            GrandTotal = subtotal
        };

        // C8: müşteri notu (daha önce sessizce düşüyordu) + sözleşme kabul kaydı tek jsonb'de.
        var notlar = new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(request.CustomerNotes))
            notlar["note"] = request.CustomerNotes!;
        if (request.AcceptedContracts is { Count: > 0 })
            notlar["acceptedContracts"] = request.AcceptedContracts;
        if (notlar.Count > 0)
            order.CustomerNotes = notlar;

        db.Orders.Add(order);

        foreach (var item in request.Items)
        {
            db.OrderItems.Add(new OrderItemEntity
            {
                OrderId = order.Id,
                VariantId = item.VariantId,
                Sku = item.Sku,
                ProductName = item.ProductName,
                VariantInfo = item.VariantInfo,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Subtotal = item.Quantity * item.UnitPrice,
                DiscountAmount = 0,
                TaxAmount = 0,
                Total = item.Quantity * item.UnitPrice,
                Status = "pending"
            });
        }

        await db.SaveChangesAsync(ct);
        return Result.Success(order.Id);
    }
}
