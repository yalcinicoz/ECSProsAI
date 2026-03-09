using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Commands.CreateOrder;

public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Result<string>>
{
    private readonly IOrderDbContext _context;

    public CreateOrderCommandHandler(IOrderDbContext context)
    {
        _context = context;
    }

    public async Task<Result<string>> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
            return Result.Failure<string>("Sipariş en az bir ürün içermelidir.");

        var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";

        var subtotal = request.Items.Sum(i => i.UnitPrice * i.Quantity);

        var order = new Domain.Entities.Order
        {
            OrderNumber = orderNumber,
            FirmPlatformId = request.FirmPlatformId,
            MemberId = request.MemberId,
            OrderType = request.OrderType,
            Status = "pending",
            PaymentStatus = "pending",
            CurrencyCode = request.CurrencyCode,
            InvoiceCurrencyCode = request.CurrencyCode,
            ExchangeRate = 1.00m,
            ShippingRecipientName = request.ShippingRecipientName,
            ShippingRecipientPhone = request.ShippingRecipientPhone,
            ShippingCountryId = request.ShippingCountryId,
            ShippingCityId = request.ShippingCityId,
            ShippingDistrictId = request.ShippingDistrictId,
            ShippingAddressLine = request.ShippingAddressLine,
            ShippingPostalCode = request.ShippingPostalCode,
            Subtotal = subtotal,
            TotalDiscount = 0,
            TotalExpense = 0,
            TotalTax = 0,
            GrandTotal = subtotal
        };

        foreach (var item in request.Items)
        {
            order.Items.Add(new OrderItem
            {
                VariantId = item.VariantId,
                Sku = string.Empty, // Catalog entegrasyonu ile doldurulacak
                ProductName = string.Empty,
                VariantInfo = string.Empty,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Subtotal = item.UnitPrice * item.Quantity,
                DiscountAmount = 0,
                TaxAmount = 0,
                Total = item.UnitPrice * item.Quantity,
                Status = "pending"
            });
        }

        _context.Orders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(orderNumber);
    }
}
