using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.CreateOrder;

public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Result<string>>
{
    private readonly IOrderDbContext _context;
    private readonly IOrderNumberService _orderNumbers;
    private readonly ECSPros.Shared.Contracts.IProductService _products;

    public CreateOrderCommandHandler(
        IOrderDbContext context,
        IOrderNumberService orderNumbers,
        ECSPros.Shared.Contracts.IProductService products)
    {
        _context = context;
        _orderNumbers = orderNumbers;
        _products = products;
    }

    public async Task<Result<string>> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
            return Result.Failure<string>("Sipariş en az bir ürün içermelidir.");

        var isExternal = !string.IsNullOrWhiteSpace(request.ExternalOrderNumber);
        var orderNumber = isExternal
            ? request.ExternalOrderNumber!.Trim()
            : await _orderNumbers.GenerateAsync(request.FirmPlatformId, cancellationToken);

        // Dış numarada çakışma ön kontrolü (yarış durumunda unique index son emniyettir)
        if (isExternal && await _context.Orders.AnyAsync(
                o => o.FirmPlatformId == request.FirmPlatformId && o.OrderNumber == orderNumber,
                cancellationToken))
            return Result.Failure<string>($"Bu kanalda '{orderNumber}' numaralı sipariş zaten kayıtlı.");

        var subtotal = request.Items.Sum(i => i.UnitPrice * i.Quantity);

        var order = new Domain.Entities.Order
        {
            OrderNumber = orderNumber,
            OrderNumberSource = isExternal ? "external" : "internal",
            ExternalOrderNumber = isExternal ? orderNumber : null,
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
            var bilgi = await _products.GetVariantAsync(item.VariantId, cancellationToken);
            order.Items.Add(new OrderItem
            {
                VariantId = item.VariantId,
                SupplierId = bilgi?.SupplierId,
                Sku = bilgi?.Sku ?? string.Empty,
                ProductName = bilgi?.ProductName ?? string.Empty,
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
