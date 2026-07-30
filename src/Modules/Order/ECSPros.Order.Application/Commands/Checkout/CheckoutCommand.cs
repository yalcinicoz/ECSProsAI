using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using OrderEntity = ECSPros.Order.Domain.Entities.Order;
using OrderItemEntity = ECSPros.Order.Domain.Entities.OrderItem;

namespace ECSPros.Order.Application.Commands.Checkout;

public record CheckoutCommand(
    Guid FirmPlatformId,
    Guid? MemberId, // 2026-07-22: misafir checkout'ta null
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
    List<AcceptedContract>? AcceptedContracts = null,
    // 2026-07-22: müşterinin teslimat adımında seçtiği kargo (mahalle bazlı seçenekler)
    Guid? RequestedCargoIntegrationId = null,
    string? RequestedCargoName = null,
    string? PaymentMethod = null,      // kart | kapida-nakit | kapida-kart (2026-07-30)
    decimal? CouponDiscount = null) : IRequest<Result<CheckoutSonucu>>;

/// <summary>Checkout dönüşü (2026-07-30): OrderNumber da döner — onay ekranı insan
/// okunur numarayı (kanal bazlı seri, F1-F5 kod sistemi) GUID'e düşmeden gösterir
/// (misafir siparişte üye-listesi geri araması yapılamıyordu).</summary>
public record CheckoutSonucu(Guid OrderId, string OrderNumber);

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

public class CheckoutCommandHandler(
    IOrderDbContext db,
    IOrderNumberService orderNumbers,
    ECSPros.Shared.Contracts.IProductService productService,
    ECSPros.Shared.Contracts.IChannelProductFlagService flagService)
    : IRequestHandler<CheckoutCommand, Result<CheckoutSonucu>>
{
    public async Task<Result<CheckoutSonucu>> Handle(CheckoutCommand request, CancellationToken ct)
    {
        if (!request.Items.Any())
            return Result.Failure<CheckoutSonucu>("Sepet boş.");

        // Kanal seçimi/durdurma (M2/M3): bu kanalda çıkarılan/durdurulan ürün siparişe geçemez.
        var kanalDisi = await flagService.GetChannelExcludedProductIdsAsync(request.FirmPlatformId, ct);

        // Katman 1 (global satış anahtarı): satışa kapalı ürün SATILAMAZ — kanal ayarları ne
        // olursa olsun. ProductInfo.IsActive = varyant aktif VE ürün IsSaleOpen. Kapalı ürün
        // sepette kalmışsa (kapatılmadan önce eklenmiş) sipariş oluşturulmaz.
        var tedarikciByVariant = new Dictionary<Guid, Guid?>();
        foreach (var item in request.Items)
        {
            var bilgi = await productService.GetVariantAsync(item.VariantId, ct);
            if (bilgi is null || !bilgi.IsActive)
                return Result.Failure<CheckoutSonucu>($"'{item.ProductName}' şu an satışa kapalı; siparişi tamamlamak için sepetten çıkarın.");
            if (kanalDisi.Contains(bilgi.ProductId))
                return Result.Failure<CheckoutSonucu>($"'{item.ProductName}' şu an bu kanalda satışa kapalı; siparişi tamamlamak için sepetten çıkarın.");
            tedarikciByVariant[item.VariantId] = bilgi.SupplierId;
        }

        var subtotal = request.Items.Sum(i => i.Quantity * i.UnitPrice);

        // 2026-07-30: kapıda ödeme hizmet bedeli SUNUCUDA hesaplanır (istemciden tutar
        // alınmaz — ödeme sayfasındaki +50 TL bilgisi bu sabitin görüntüsüdür) ve sipariş
        // toplamına yazılır; 3.000 TL üstü kapıda ödeme sunucuda da reddedilir. Kupon
        // indirimi de artık sipariş toplamına yansır (önceden yalnız kullanım kaydıydı).
        const decimal kapidaOdemeBedeli = 50m;
        const decimal kapidaOdemeUstSinir = 3000m;
        var kapidaOdeme = request.PaymentMethod is "kapida-nakit" or "kapida-kart";
        var indirim = Math.Clamp(request.CouponDiscount ?? 0m, 0m, subtotal);
        var masraf = kapidaOdeme ? kapidaOdemeBedeli : 0m;
        if (kapidaOdeme && subtotal - indirim >= kapidaOdemeUstSinir)
            return Result.Failure<CheckoutSonucu>("3.000 TL ve üzeri siparişlerde kapıda ödeme kabul edilmez; lütfen kart ile ödemeyi seçin.");

        var orderNumber = await orderNumbers.GenerateAsync(request.FirmPlatformId, ct);

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
            RequestedCargoIntegrationId = request.RequestedCargoIntegrationId,
            RequestedCargoName = request.RequestedCargoName,
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
            TotalDiscount = indirim,
            TotalExpense = masraf,
            TotalTax = 0,
            GrandTotal = subtotal - indirim + masraf
        };

        // C8: müşteri notu (daha önce sessizce düşüyordu) + sözleşme kabul kaydı tek jsonb'de.
        var notlar = new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(request.CustomerNotes))
            notlar["note"] = request.CustomerNotes!;
        if (request.AcceptedContracts is { Count: > 0 })
            notlar["acceptedContracts"] = request.AcceptedContracts;
        if (!string.IsNullOrWhiteSpace(request.PaymentMethod))
            notlar["paymentMethod"] = request.PaymentMethod!; // şemasız kayıt (jsonb) — kolon açmadan yöntem izi
        if (notlar.Count > 0)
            order.CustomerNotes = notlar;

        db.Orders.Add(order);

        foreach (var item in request.Items)
        {
            db.OrderItems.Add(new OrderItemEntity
            {
                OrderId = order.Id,
                VariantId = item.VariantId,
                SupplierId = tedarikciByVariant.GetValueOrDefault(item.VariantId),
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
        return Result.Success(new CheckoutSonucu(order.Id, orderNumber));
    }
}
