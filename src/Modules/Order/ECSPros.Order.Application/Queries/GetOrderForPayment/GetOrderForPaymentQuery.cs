using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetOrderForPayment;

/// <summary>PayTR ödeme başlatma için gereken sipariş alanları (2026-07-30).
/// Tutar KURUŞ cinsinden (long) döner — dahili kanonik tam sayı. NOT: PayTR Direct API
/// (/odeme) payment_amount'ı TL ONDALIK ister ("24.99"); dönüşüm PaymentController'da
/// (TutarKurus/100). (Kuruş integer yalnız iFrame API içindir.)</summary>
public record GetOrderForPaymentQuery(Guid OrderId) : IRequest<Result<OrderPaymentInfo>>;

public record OrderPaymentInfo(
    Guid OrderId,
    string OrderNumber,
    Guid? MemberId,
    long TutarKurus,
    string CurrencyCode,
    string PaymentStatus,
    string AliciAd,
    string AliciTelefon);

public class GetOrderForPaymentQueryHandler(IOrderDbContext db)
    : IRequestHandler<GetOrderForPaymentQuery, Result<OrderPaymentInfo>>
{
    public async Task<Result<OrderPaymentInfo>> Handle(GetOrderForPaymentQuery request, CancellationToken ct)
    {
        var o = await db.Orders
            .Where(x => x.Id == request.OrderId)
            .Select(x => new
            {
                x.Id, x.OrderNumber, x.MemberId, x.GrandTotal, x.CurrencyCode,
                x.PaymentStatus, x.ShippingRecipientName, x.ShippingRecipientPhone
            })
            .FirstOrDefaultAsync(ct);
        if (o is null) return Result.Failure<OrderPaymentInfo>("Sipariş bulunamadı.");

        var kurus = (long)Math.Round(o.GrandTotal * 100m, MidpointRounding.AwayFromZero);
        return Result.Success(new OrderPaymentInfo(
            o.Id, o.OrderNumber, o.MemberId, kurus,
            string.IsNullOrWhiteSpace(o.CurrencyCode) ? "TRY" : o.CurrencyCode,
            o.PaymentStatus, o.ShippingRecipientName, o.ShippingRecipientPhone));
    }
}
