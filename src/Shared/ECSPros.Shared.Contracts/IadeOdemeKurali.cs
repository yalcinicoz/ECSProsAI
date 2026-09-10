namespace ECSPros.Shared.Contracts;

/// <summary>
/// Müşteriye para iadesi uygunluğu — TEK kural (İade akışı planı §2.5, 2026-09-10, kullanıcı kararları R7-R10).
///
/// Kararlar:
///  • R7  Para iadesi YALNIZ müşteriden tahsilat yapıldıysa hesaplanır (kart/havale/cüzdan: checkout anında).
///  • R8  Teslim edilmeden iade alınan KAPIDA ÖDEME siparişinde tahsilat yoktur → kesinlikle para iadesi yok.
///  • R9  Teslim edilmiş kapıda ödemede tahsilat yapılmıştır (teslimde tahsilat kaydı atılır, §2.6) → uygundur.
///  • R10 PAZARYERİ siparişinde hiçbir durumda para iadesi yapılmaz; iadeyi pazaryeri yapar.
///
/// Kural ÖDEME YÖNTEMİNE değil TAHSİLAT KAYDINA bakar: tahsilat varsa uygun, yoksa değildir. Ödeme yöntemi
/// yalnız "neden" metnini seçer (kapıda ödeme → <c>cod_not_collected</c>, diğerleri → <c>unpaid</c>).
///
/// Uygulama noktaları: iade OLUŞTURMA (RefundStatus/RefundNotApplicableReason yazılır) ve
/// GERİ ÖDEME TAMAMLAMA (ikinci savunma hattı: uygun değilse ya da tutar üst sınırı aşıyorsa hata).
/// </summary>
public static class IadeOdemeKurali
{
    public const string NedenPazaryeri = "marketplace";
    public const string NedenKapidaTahsilatYok = "cod_not_collected";
    public const string NedenTahsilatYok = "unpaid";
    public const string NedenZatenIadeEdildi = "already_refunded";

    /// <summary>Kural girdisi.</summary>
    /// <param name="KanalPazaryeri">Siparişin kanalı pazaryeri mi (core_platform_types.IsMarketplace).</param>
    /// <param name="TahsilEdilen">Müşteriden fiilen tahsil edilen tutar (<see cref="TahsilEdilen"/> ile hesaplanır).</param>
    /// <param name="DahaOnceIadeEdilen">Aynı siparişin tamamlanmış geri ödemeleri toplamı (diğer iadeler).</param>
    /// <param name="OdemeYontemi">kart | kapida-nakit | kapida-kart | null — yalnız neden metni için.</param>
    /// <param name="TeslimEdildi">İade anında sipariş teslim edilmiş miydi (bilgi amaçlı; karar tahsilata bakar).</param>
    public sealed record Girdi(
        bool KanalPazaryeri,
        decimal TahsilEdilen,
        decimal DahaOnceIadeEdilen,
        string? OdemeYontemi,
        bool TeslimEdildi);

    /// <summary>Sonuç: uygun mu, değilse neden kodu, uygunsa müşteriye ödenebilecek ÜST SINIR.</summary>
    public sealed record Sonuc(bool Uygun, string? Neden, decimal UstSinir);

    public static Sonuc Degerlendir(Girdi g)
    {
        if (g.KanalPazaryeri)
            return new(false, NedenPazaryeri, 0m);

        if (g.TahsilEdilen <= 0m)
            return new(false, KapidaOdeme(g.OdemeYontemi) ? NedenKapidaTahsilatYok : NedenTahsilatYok, 0m);

        var kalan = Math.Round(g.TahsilEdilen - Math.Max(0m, g.DahaOnceIadeEdilen), 2, MidpointRounding.AwayFromZero);
        return kalan > 0m ? new(true, null, kalan) : new(false, NedenZatenIadeEdildi, 0m);
    }

    /// <summary>Kapıda ödeme yöntemi mi (nakit ya da kart fark etmez — tahsilat teslimde olur).</summary>
    public static bool KapidaOdeme(string? odemeYontemi)
        => odemeYontemi is "kapida-nakit" or "kapida-kart";

    /// <summary>
    /// Müşteriden tahsil edilen tutar. Birincil kaynak tamamlanmış ödeme satırları (<c>ord_order_payments</c>
    /// Status=completed). Bu plandan ÖNCE oluşan kart siparişlerinde ödeme satırı yoktu, yalnız
    /// <c>Order.PaymentStatus=paid</c> vardı (canlıda 171 sipariş / 0 ödeme satırı) — geriye dönük uyum için
    /// satır yoksa ve durum <c>paid</c> ise sipariş toplamı tahsil edilmiş sayılır. Yeni siparişlerde
    /// PayTR/mock/teslimde-tahsilat ödeme satırı yazdığı için birincil yol çalışır.
    /// </summary>
    public static decimal TahsilEdilen(decimal tamamlananOdemelerToplami, string? paymentStatus, decimal siparisToplami)
    {
        if (tamamlananOdemelerToplami > 0m) return tamamlananOdemelerToplami;
        return string.Equals(paymentStatus, "paid", StringComparison.OrdinalIgnoreCase) ? siparisToplami : 0m;
    }

    /// <summary>İade edilecek tutar: kalemler toplamı, üst sınırla kırpılır (E9 — sınır aşılamaz).</summary>
    public static decimal TutarKirp(decimal istenen, decimal ustSinir)
        => Math.Round(Math.Clamp(istenen, 0m, Math.Max(0m, ustSinir)), 2, MidpointRounding.AwayFromZero);
}
