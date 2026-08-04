using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ECSPros.Order.Application.Commands.PayTrPayment;

/// <summary>PayTR ödeme başlatma anında siparişe ödeme izini yazar (2026-07-30).
/// GÜVENLİK: yalnız MASKELİ PAN (ilk6+son4) alınır — tam kart no/CVV bu komuta HİÇ gelmez.
/// Order.CustomerNotes jsonb'sine "payment" anahtarıyla yazılır (şemasız, kolon açılmaz).</summary>
public record PayTrPaymentBaslatCommand(Guid OrderId, string MaskeliPan, bool TestMode)
    : IRequest<Result>;

/// <summary>PayTR callback sonucu siparişe uygulanır: başarıda PaymentStatus=paid,
/// başarısızda failed. merchant_oid = OrderNumber. Idempotent (tekrar gelen callback
/// mevcut sonucu bozmaz). Bu komut YALNIZ hash doğrulanmış callback'ten çağrılır.</summary>
public record PayTrCallbackUygulaCommand(
    string MerchantOid, bool Basarili, string? BasarisizlikNedeni, string? TotalAmount = null,
    bool AutoConfirm = true)   // O2 (2026-08-04): kart onay politikası gerektiriyorsa false —
    : IRequest<Result<Guid>>;  // sipariş paid+pending kalır, müşteri onayı beklenir

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

public class PayTrCallbackUygulaCommandHandler(
    IOrderDbContext db, IPublisher publisher, ILogger<PayTrCallbackUygulaCommandHandler> logger)
    : IRequestHandler<PayTrCallbackUygulaCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(PayTrCallbackUygulaCommand request, CancellationToken ct)
    {
        // Onay adımında rezervasyon için kalemler gerekir (Order.Confirm Items üzerinden event kurar).
        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNumber == request.MerchantOid, ct);
        if (order is null) return Result.Failure<Guid>("Sipariş bulunamadı.");

        // Idempotent: zaten paid/underpaid (terminal) ise tekrar işleme (PayTR callback'i tekrar gelebilir).
        if (order.PaymentStatus is "paid" or "underpaid") return Result.Success(order.Id);

        // ★ GÜVENLİK (2026-07-31): EKSİK ÖDEME kontrolü — PayTR'nin işlediği tutar (callback
        // total_amount) sipariş tutarından (GrandTotal) düşükse ödeme "eksik" işaretlenir, sipariş
        // ONAYLANMAZ (operasyona düşmez). PayTR callback total_amount genelde KURUŞ; Direct API TL
        // echo'su ihtimaline karşı iki yorum da denenir, GrandTotal'a en yakın olan alınır.
        bool eksikOdeme = false;
        if (request.Basarili && decimal.TryParse(request.TotalAmount,
                System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var ta) && ta > 0)
        {
            var kurusYorum = ta / 100m;   // PayTR standart callback = kuruş
            var tlYorum = ta;             // Direct API TL echo ihtimali
            var odenen = Math.Abs(kurusYorum - order.GrandTotal) <= Math.Abs(tlYorum - order.GrandTotal)
                ? kurusYorum : tlYorum;
            eksikOdeme = odenen + 0.02m < order.GrandTotal;   // tahsil edilen, sipariş tutarından düşük
            if (eksikOdeme)
                logger.LogWarning("PayTR callback EKSİK ÖDEME: OrderNumber={Oid} sipariş={GT} tahsil={Odenen} (total_amount={Ta})",
                    request.MerchantOid, order.GrandTotal, odenen, request.TotalAmount);
        }

        var mevcut = order.CustomerNotes is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object>(order.CustomerNotes);
        var odeme = mevcut.TryGetValue("payment", out var p) && p is Dictionary<string, object?> d
            ? new Dictionary<string, object?>(d)
            : new Dictionary<string, object?> { ["provider"] = "paytr" };
        odeme["status"] = !request.Basarili ? "failed" : eksikOdeme ? "underpaid" : "success";
        if (!request.Basarili && !string.IsNullOrWhiteSpace(request.BasarisizlikNedeni))
            odeme["failReason"] = request.BasarisizlikNedeni;
        if (eksikOdeme)
        {
            odeme["expectedAmount"] = order.GrandTotal;
            odeme["paidTotalAmount"] = request.TotalAmount;
        }
        mevcut["payment"] = odeme;
        order.CustomerNotes = mevcut;
        // Eksik ödeme "paid" sayılmaz → operasyona düşmez; personel inceler.
        order.PaymentStatus = !request.Basarili ? "failed" : eksikOdeme ? "underpaid" : "paid";
        await db.SaveChangesAsync(ct);

        // Ödeme başarılı VE TAM ise siparişi OTOMATİK ONAYLA (pending → confirmed). Onay,
        // OrderConfirmedEvent ile online stok rezervasyonunu tetikler (WarehouseId=Guid.Empty →
        // depo-bağımsız). Onay hatası ödeme kaydını BLOKLAMAZ; log'a düşer, personel elle onaylayabilir.
        // EKSİK ödeme onaylanmaz (Status pending kalır) — kasıtlı: tutar uyuşmazlığı operasyona girmesin.
        if (request.Basarili && !eksikOdeme && request.AutoConfirm)
        {
            try
            {
                order.Confirm(Guid.Empty, Guid.Empty);   // warehouseId=Empty → online cross-warehouse reserve
                await db.SaveChangesAsync(ct);
                foreach (var domainEvent in order.DomainEvents)
                    await publisher.Publish(domainEvent, ct);
                order.ClearDomainEvents();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "PayTR callback: ödeme alındı (paid) ama sipariş otomatik onaylanamadı (OrderNumber={Oid}).",
                    request.MerchantOid);
            }
        }

        return Result.Success(order.Id);
    }
}
