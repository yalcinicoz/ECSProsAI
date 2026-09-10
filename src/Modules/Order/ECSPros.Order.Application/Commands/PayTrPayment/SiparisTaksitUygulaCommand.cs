using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.PayTrPayment;

/// <summary>
/// Ödeme başlatılırken seçilen taksit ve MÜŞTERİYE yansıtılan vade farkını siparişe yazar (2026-09-10).
/// Vade farkı kanalın kendi taksit tablosundan TaksitKurali ile SUNUCUDA hesaplanmış gelir (istemci tutarı
/// yok sayılır). Tekrar denemede (başarısız 3D sonrası başka taksit seçimi) eski vade farkı düşülüp yenisi
/// eklenir — GrandTotal = önceki GrandTotal − eski fee + yeni fee. Ödenmiş siparişte değiştirilemez.
/// Dönen değer: yeni GrandTotal (PayTR'ye gönderilecek tutar).
/// </summary>
public record SiparisTaksitUygulaCommand(Guid OrderId, int InstallmentCount, decimal InstallmentFee)
    : IRequest<Result<decimal>>;

public class SiparisTaksitUygulaCommandHandler(IOrderDbContext db)
    : IRequestHandler<SiparisTaksitUygulaCommand, Result<decimal>>
{
    public async Task<Result<decimal>> Handle(SiparisTaksitUygulaCommand request, CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, ct);
        if (order is null) return Result.Failure<decimal>("Sipariş bulunamadı.");
        if (order.PaymentStatus == "paid") return Result.Failure<decimal>("Ödenmiş siparişte taksit değiştirilemez.");
        if (request.InstallmentFee < 0) return Result.Failure<decimal>("Vade farkı negatif olamaz.");

        var adet = request.InstallmentCount <= 1 ? 1 : request.InstallmentCount;
        var yeniFee = adet == 1 ? 0m : Math.Round(request.InstallmentFee, 2, MidpointRounding.AwayFromZero);
        var eskiFee = order.InstallmentFee;
        if (order.InstallmentCount == adet && eskiFee == yeniFee) return Result.Success(order.GrandTotal);

        order.InstallmentCount = adet;
        order.InstallmentFee = yeniFee;
        order.GrandTotal = Math.Round(order.GrandTotal - eskiFee + yeniFee, 2, MidpointRounding.AwayFromZero);
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(order.GrandTotal);
    }
}
