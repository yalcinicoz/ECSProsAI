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
    Guid? CartId = null) : IRequest<Result<Guid>>;

public record CheckoutItem(
    Guid VariantId,
    string Sku,
    string ProductName,
    string VariantInfo,
    int Quantity,
    decimal UnitPrice);

public class CheckoutCommandHandler(IOrderDbContext db) : IRequestHandler<CheckoutCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CheckoutCommand request, CancellationToken ct)
    {
        if (!request.Items.Any())
            return Result.Failure<Guid>("Sepet boş.");

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
