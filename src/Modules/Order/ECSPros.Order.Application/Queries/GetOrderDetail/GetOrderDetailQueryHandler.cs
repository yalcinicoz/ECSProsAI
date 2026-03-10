using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetOrderDetail;

public class GetOrderDetailQueryHandler : IRequestHandler<GetOrderDetailQuery, Result<OrderDetailDto>>
{
    private readonly IOrderDbContext _context;

    public GetOrderDetailQueryHandler(IOrderDbContext context)
    {
        _context = context;
    }

    public async Task<Result<OrderDetailDto>> Handle(GetOrderDetailQuery request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order is null)
            return Result.Failure<OrderDetailDto>("Sipariş bulunamadı.");

        return Result.Success(new OrderDetailDto(
            order.Id,
            order.OrderNumber,
            order.MemberId,
            order.Status,
            order.PaymentStatus,
            order.OrderType,
            order.CurrencyCode,
            order.Subtotal,
            order.TotalDiscount,
            order.TotalTax,
            order.GrandTotal,
            order.ShippingRecipientName,
            order.ShippingRecipientPhone,
            order.ShippingAddressLine,
            order.PickingPlanId,
            order.InternalNotes,
            order.CreatedAt,
            order.ConfirmedAt,
            order.Items.Select(i => new OrderDetailItemDto(
                i.Id, i.VariantId, i.Sku, i.ProductName, i.VariantInfo,
                i.Quantity, i.UnitPrice, i.DiscountAmount, i.TaxAmount, i.Total, i.Status)).ToList(),
            order.Payments.Select(p => new OrderDetailPaymentDto(
                p.Id, p.PaymentMethodId, p.Amount, p.CurrencyCode, p.Status)).ToList()));
    }
}
