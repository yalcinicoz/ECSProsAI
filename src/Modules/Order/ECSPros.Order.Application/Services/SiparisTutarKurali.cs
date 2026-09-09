namespace ECSPros.Order.Application.Services;

/// <summary>Sipariş para hesabının sonucu (tek kural — checkout ve ön izleme aynısını kullanır).</summary>
public readonly record struct SiparisTutari(
    decimal Subtotal,
    decimal Indirim,
    decimal Masraf,
    decimal KargoBedeli,
    string? KargoBedavaSebebi,
    decimal GenelToplam);

/// <summary>
/// M2 (2026-09-09): sipariş tutarının ARİTMETİĞİ tek yerde.
///
/// Neden: <c>POST /checkout</c> ile yeni <c>POST /checkout/preview</c> aynı sayıyı üretmek
/// ZORUNDA — ön izlemede 1.000 TL görüp siparişte 1.049 TL ödemek güvenilirliği bitirir.
/// Fiyat/kampanya/kupon çözümü servislere aittir; burada yalnız birleştirme kuralı durur:
///  • indirim = (kupon + sepet-seviyesi kampanya), ara toplamı AŞAMAZ,
///  • kapıda ödeme masrafı yalnız kapıda ödeme yöntemlerinde eklenir,
///  • kargo <see cref="ECSPros.Shared.Contracts.KargoUcretiKurali"/> ile — eşik tabanı
///    İNDİRİMLER SONRASI ürün tutarıdır (kullanıcı kararı, 2026-09-09),
///  • genel toplam = ara toplam − indirim + masraf + kargo.
/// </summary>
public static class SiparisTutarKurali
{
    public static SiparisTutari Hesapla(
        decimal subtotal,
        decimal kuponIndirimi,
        decimal kampanyaSepetIndirimi,
        bool kapidaOdeme,
        decimal kapidaOdemeMasrafi,
        decimal kanalKargoUcreti,
        decimal kanalKargoEsigi,
        decimal? kargoKampanyaUcreti)
    {
        var indirim = Math.Clamp(kuponIndirimi + kampanyaSepetIndirimi, 0m, subtotal);
        var masraf = kapidaOdeme ? kapidaOdemeMasrafi : 0m;

        var kargo = ECSPros.Shared.Contracts.KargoUcretiKurali.Hesapla(
            kanalKargoUcreti, kanalKargoEsigi, subtotal - indirim, kargoKampanyaUcreti);

        return new SiparisTutari(
            subtotal, indirim, masraf, kargo.Ucret, kargo.BedavaSebebi,
            subtotal - indirim + masraf + kargo.Ucret);
    }
}
