using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.PayTrPayment;

/// <summary>PayTR ödeme başlatma anında siparişe ödeme izini yazar (2026-07-30).
/// GÜVENLİK: yalnız MASKELİ PAN (ilk6+son4) alınır — tam kart no/CVV bu komuta HİÇ gelmez.
/// Order.CustomerNotes jsonb'sine "payment" anahtarıyla yazılır (şemasız, kolon açılmaz).</summary>
public record PayTrPaymentBaslatCommand(Guid OrderId, string MaskeliPan, bool TestMode)
    : IRequest<Result>;

/// <summary>PayTR callback sonucu siparişe uygulanır: başarıda PaymentStatus=paid,
/// başarısızda failed. merchant_oid = OrderNumber. Idempotent (tekrar gelen callback
/// mevcut sonucu bozmaz). Bu komut YALNIZ hash doğrulanmış callback'ten çağrılır.</summary>
public record PayTrCallbackUygulaCommand(string MerchantOid, bool Basarili, string? BasarisizlikNedeni)
    : IRequest<Result<Guid>>;

public class PayTrPaymentBaslatCommandHandler(IOrderDbContext db)
    : IRequestHandler<PayTrPaymentBaslatCommand, Result>
{
    public async Task<Result> Handle(PayTrPaymentBaslatCommand request, CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, ct);
        if (order is null) return Result.Failure("Sipariş bulunamadı.");

        var notlar = order.CustomerNotes is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object>(order.CustomerNotes);
        notlar["payment"] = new Dictionary<string, object?>
        {
            ["provider"] = "paytr",
            ["status"] = "pending",
            ["maskedPan"] = request.MaskeliPan,   // yalnız maskeli — tam PAN/CVV yok
            ["testMode"] = request.TestMode,
        };
        order.CustomerNotes = notlar;
        order.PaymentStatus = "pending";
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public class PayTrCallbackUygulaCommandHandler(IOrderDbContext db)
    : IRequestHandler<PayTrCallbackUygulaCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(PayTrCallbackUygulaCommand request, CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.OrderNumber == request.MerchantOid, ct);
        if (order is null) return Result.Failure<Guid>("Sipariş bulunamadı.");

        // Idempotent: zaten paid ise tekrar işleme (PayTR callback'i birden çok gelebilir).
        if (order.PaymentStatus == "paid") return Result.Success(order.Id);

        var mevcut = order.CustomerNotes is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object>(order.CustomerNotes);
        var odeme = mevcut.TryGetValue("payment", out var p) && p is Dictionary<string, object?> d
            ? new Dictionary<string, object?>(d)
            : new Dictionary<string, object?> { ["provider"] = "paytr" };
        odeme["status"] = request.Basarili ? "success" : "failed";
        if (!request.Basarili && !string.IsNullOrWhiteSpace(request.BasarisizlikNedeni))
            odeme["failReason"] = request.BasarisizlikNedeni;
        mevcut["payment"] = odeme;
        order.CustomerNotes = mevcut;
        order.PaymentStatus = request.Basarili ? "paid" : "failed";
        await db.SaveChangesAsync(ct);
        return Result.Success(order.Id);
    }
}
