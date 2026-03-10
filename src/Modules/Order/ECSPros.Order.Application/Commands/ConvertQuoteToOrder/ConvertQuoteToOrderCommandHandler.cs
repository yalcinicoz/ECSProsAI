using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.ConvertQuoteToOrder;

public class ConvertQuoteToOrderCommandHandler : IRequestHandler<ConvertQuoteToOrderCommand, Result<Guid>>
{
    private readonly IOrderDbContext _context;

    public ConvertQuoteToOrderCommandHandler(IOrderDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(ConvertQuoteToOrderCommand request, CancellationToken cancellationToken)
    {
        var quote = await _context.Quotes
            .Include(q => q.Items)
            .FirstOrDefaultAsync(q => q.Id == request.QuoteId, cancellationToken);

        if (quote is null)
            return Result.Failure<Guid>("Teklif bulunamadı.");

        if (quote.Status != "accepted")
            return Result.Failure<Guid>("Yalnızca kabul edilmiş teklifler siparişe dönüştürülebilir.");

        if (quote.ConvertedOrderId.HasValue)
            return Result.Failure<Guid>("Bu teklif zaten siparişe dönüştürülmüş.");

        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpper();
        var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{suffix}";

        var order = new Domain.Entities.Order
        {
            OrderNumber = orderNumber,
            FirmPlatformId = quote.FirmPlatformId,
            MemberId = quote.MemberId,
            Status = "pending",
            PaymentStatus = "pending",
            OrderType = "b2b",
            CurrencyCode = quote.CurrencyCode,
            Subtotal = quote.Subtotal,
            TotalDiscount = quote.TotalDiscount,
            TotalTax = quote.TotalTax,
            GrandTotal = quote.GrandTotal,
            ShippingRecipientName = request.ShippingRecipientName,
            ShippingRecipientPhone = request.ShippingRecipientPhone,
            ShippingCountryId = request.ShippingCountryId,
            ShippingCityId = request.ShippingCityId,
            ShippingDistrictId = request.ShippingDistrictId,
            ShippingAddressLine = request.ShippingAddressLine,
            ShippingPostalCode = request.ShippingPostalCode,
            QuoteId = quote.Id,
            InternalNotes = $"Teklif dönüşümü: {quote.QuoteNumber}",
            CreatedBy = request.ConvertedBy
        };

        foreach (var qi in quote.Items)
        {
            order.Items.Add(new OrderItem
            {
                VariantId = qi.VariantId,
                Sku = qi.Sku,
                ProductName = qi.ProductName,
                VariantInfo = qi.VariantInfo,
                Quantity = qi.Quantity,
                UnitPrice = qi.UnitPrice,
                DiscountAmount = qi.DiscountAmount,
                TaxAmount = qi.TaxAmount,
                Total = qi.Total,
                Subtotal = qi.UnitPrice * qi.Quantity,
                Status = "pending"
            });
        }

        order.Payments.Add(new OrderPayment
        {
            PaymentMethodId = request.PaymentMethodId,
            Amount = quote.GrandTotal,
            CurrencyCode = quote.CurrencyCode,
            Status = "pending"
        });

        _context.Orders.Add(order);

        quote.Status = "converted";
        quote.ConvertedOrderId = order.Id;
        quote.UpdatedAt = DateTime.UtcNow;
        quote.UpdatedBy = request.ConvertedBy;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(order.Id);
    }
}
