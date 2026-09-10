using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Contracts.Channels;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Services;

/// <summary>
/// <see cref="IadeOdemeKurali"/> girdilerini sipariş/kanal/ödeme kayıtlarından toplar ve kuralı uygular
/// (İade planı §2.5). Kuralın kendisi Shared.Contracts'ta — burası yalnız veri toplama.
/// Kullanım: iade oluşturma (RefundStatus yazımı), geri ödeme tamamlama (ikinci savunma), iade detayı (üst sınır).
/// </summary>
public static class IadeOdemeDegerlendirme
{
    public static async Task<IadeOdemeKurali.Sonuc> DegerlendirAsync(
        IOrderDbContext db, IChannelCapabilityResolver kanal, Domain.Entities.Order order,
        Guid? haricIadeId, CancellationToken ct)
    {
        var pazaryeri = await kanal.IsMarketplaceAsync(order.FirmPlatformId, ct);
        var odemeler = await Tahsilat.TamamlananToplamAsync(db, order.Id, ct);
        var tahsilEdilen = IadeOdemeKurali.TahsilEdilen(odemeler, order.PaymentStatus, order.GrandTotal);

        // Aynı siparişin DİĞER iadelerinde tamamlanmış geri ödemeler (bu iade hariç — tekrar sayılmasın).
        var dahaOnce = await db.ReturnRefunds.AsNoTracking()
            .Where(r => r.Status == "completed" && r.Return.OrderId == order.Id
                        && (haricIadeId == null || r.ReturnId != haricIadeId.Value))
            .SumAsync(r => (decimal?)r.Amount, ct) ?? 0m;

        return IadeOdemeKurali.Degerlendir(new IadeOdemeKurali.Girdi(
            pazaryeri, tahsilEdilen, dahaOnce, order.PaymentMethod, order.Status == "delivered"));
    }

    /// <summary>Sonucu iade kaydına yazar: uygun → pending; değil → not_applicable + neden, RefundAmount 0.</summary>
    public static void Uygula(Return iade, IadeOdemeKurali.Sonuc sonuc)
    {
        if (sonuc.Uygun)
        {
            iade.RefundStatus = ReturnConstants.RefundPending;
            iade.RefundNotApplicableReason = null;
            iade.RefundAmount = IadeOdemeKurali.TutarKirp(iade.RefundAmount, sonuc.UstSinir);
        }
        else
        {
            iade.RefundStatus = ReturnConstants.RefundNotApplicable;
            iade.RefundNotApplicableReason = sonuc.Neden;
            iade.RefundAmount = 0m;
            if (iade.RefundMethod == ReturnConstants.RefundMethodOriginalPayment || string.IsNullOrEmpty(iade.RefundMethod))
                iade.RefundMethod = ReturnConstants.RefundMethodNone;
        }
    }

    /// <summary>Kalem geri ödeme tutarı: kampanya dağıtımı sonrası gerçek ödenen (OrderItem.Total) + vade farkı payı.</summary>
    public static (decimal Birim, decimal Toplam) KalemTutari(OrderItem kalem, int adet, decimal vadeFarkiPayi)
    {
        if (kalem.Quantity <= 0 || adet <= 0) return (0m, 0m);
        var birim = Math.Round((kalem.Total + vadeFarkiPayi) / kalem.Quantity, 2, MidpointRounding.AwayFromZero);
        var toplam = adet == kalem.Quantity ? kalem.Total + vadeFarkiPayi : Math.Round(birim * adet, 2, MidpointRounding.AwayFromZero);
        return (birim, toplam);
    }
}
