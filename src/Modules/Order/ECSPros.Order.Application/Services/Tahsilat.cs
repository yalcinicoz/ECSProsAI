using ECSPros.Order.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Services;

/// <summary>
/// Tahsilat kaydı yardımcıları (İade planı §2.6, 2026-09-10). <c>Order.PaymentStatus</c> TÜRETİLMİŞ özettir;
/// "tahsilat yapıldı mı" sorusunun kaynağı <c>ord_order_payments</c> tamamlanmış satırlarıdır.
/// </summary>
public static class Tahsilat
{
    public const string StatusCompleted = "completed";
    public const string KaynakTeslimdeKapidaOdeme = "cod_on_delivery";
    public const string KaynakPayTr = "paytr";
    public const string KaynakMock = "mock";

    /// <summary>Siparişin tamamlanmış ödeme satırları toplamı (soft-delete filtresi DbSet'te).</summary>
    public static async Task<decimal> TamamlananToplamAsync(IOrderDbContext db, Guid orderId, CancellationToken ct)
        => await db.OrderPayments.AsNoTracking()
            .Where(p => p.OrderId == orderId && p.Status == StatusCompleted)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

    /// <summary>Tahsilat satırı ekler ve PaymentStatus'u tamamlanmış toplama göre günceller (paid | partial).
    /// Aynı kaynaktan ikinci satır yazılmaz (idempotent: PayTR callback'i / teslim komutu tekrar gelebilir).</summary>
    public static async Task<OrderPayment?> KaydetAsync(
        IOrderDbContext db, Domain.Entities.Order order, Guid paymentMethodId, decimal amount, string kaynak,
        Guid? createdBy, CancellationToken ct, Dictionary<string, object>? ekDetay = null)
    {
        if (amount <= 0m) return null;

        var mevcutlar = await db.OrderPayments
            .Where(p => p.OrderId == order.Id && p.Status == StatusCompleted)
            .ToListAsync(ct);
        if (mevcutlar.Any(p => p.Details != null && p.Details.TryGetValue("source", out var s) && s?.ToString() == kaynak))
            return null;

        var details = new Dictionary<string, object> { ["source"] = kaynak, ["method"] = order.PaymentMethod ?? "" };
        if (ekDetay is not null) foreach (var (k, v) in ekDetay) details[k] = v;

        var payment = new OrderPayment
        {
            OrderId = order.Id,
            PaymentMethodId = paymentMethodId,
            Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero),
            CurrencyCode = string.IsNullOrWhiteSpace(order.CurrencyCode) ? "TRY" : order.CurrencyCode,
            Status = StatusCompleted,
            Details = details,
            CreatedBy = createdBy
        };
        db.OrderPayments.Add(payment);

        var toplam = mevcutlar.Sum(p => p.Amount) + payment.Amount;
        order.PaymentStatus = PaymentStatusFor(toplam, order.GrandTotal);
        return payment;
    }

    /// <summary>Tamamlanmış toplam ≥ sipariş toplamı → paid; 0 &lt; toplam → partial; aksi unpaid.</summary>
    public static string PaymentStatusFor(decimal tamamlananToplam, decimal grandTotal)
        => tamamlananToplam + 0.005m >= grandTotal ? "paid" : tamamlananToplam > 0m ? "partial" : "unpaid";
}
