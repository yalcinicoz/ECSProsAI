using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetOrderDetail;

public class GetOrderDetailQueryHandler : IRequestHandler<GetOrderDetailQuery, Result<OrderDetailDto>>
{
    private readonly IOrderDbContext _context;
    private readonly ECSPros.Shared.Contracts.IProductService _productService;

    public GetOrderDetailQueryHandler(IOrderDbContext context, ECSPros.Shared.Contracts.IProductService productService)
    {
        _context = context;
        _productService = productService;
    }

    public async Task<Result<OrderDetailDto>> Handle(GetOrderDetailQuery request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .AsSplitQuery()   // Faz 2: kardeş koleksiyon Include kartezyeni önlenir
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order is null)
            return Result.Failure<OrderDetailDto>("Sipariş bulunamadı.");

        // A1 (2026-09-07): kalem zenginleştirme (ürün kodu, varyant görseli, renk/beden kimliği) — tek toplu
        // Catalog sorgusu; hata-güvenli (katalog erişilemezse kalemler zenginleştirilmeden döner).
        var gosterim = new Dictionary<Guid, ECSPros.Shared.Contracts.VariantDisplayInfo>();
        try
        {
            gosterim = await _productService.GetVariantDisplayAsync(
                order.Items.Select(i => i.VariantId).Distinct().ToList(), cancellationToken);
        }
        catch { /* zenginleştirme isteğe bağlı */ }

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
            order.Items.Select(i =>
            {
                gosterim.TryGetValue(i.VariantId, out var g);
                return new OrderDetailItemDto(
                    i.Id, i.VariantId, i.Sku, i.ProductName, i.VariantInfo,
                    i.Quantity, i.UnitPrice, i.DiscountAmount, i.TaxAmount, i.Total, i.Status,
                    g?.ProductCode, g?.ImageUrl, g?.ColorValueId, g?.SizeValueId);
            }).ToList(),
            order.Payments.Select(p => new OrderDetailPaymentDto(
                p.Id, p.PaymentMethodId, p.Amount, p.CurrencyCode, p.Status)).ToList(),
            order.FirmPlatformId,
            order.TotalExpense,
            order.ShippingPostalCode,
            order.ShippingDeliveryNotes,
            order.ShippingCityId,
            order.ShippingDistrictId,
            order.ShippingNeighborhoodId,
            order.BillingSameAsShipping,
            order.BillingRecipientName,
            order.BillingCompanyName,
            order.BillingTaxOffice,
            order.BillingTaxNumber,
            order.BillingAddressLine,
            order.BillingCityId,
            order.BillingDistrictId,
            order.CustomerNotes,
            order.RequestedCargoIntegrationId,
            order.RequestedCargoName,
            order.PaymentMethod,
            order.ShippingFee,
            order.InstallmentCount,
            order.InstallmentFee));
    }
}
