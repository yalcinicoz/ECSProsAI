using System.Security.Cryptography;
using System.Text;

namespace ECSPros.Shared.Contracts;

/// <summary>Bir durum kodunun insan-okur karşılığı: etiket + renk (hex) + anlamsal varyant.</summary>
/// <param name="Kod">Sözleşmedeki ham kod (<c>pending</c>, <c>shipped</c>…) — istemci mantığı BUNA bakar.</param>
/// <param name="Etiket">Ekranda gösterilecek metin.</param>
/// <param name="Renk">Hex renk (<c>#16a34a</c>) — istemcide tema yoksa doğrudan kullanılır.</param>
/// <param name="Varyant">success | warning | danger | neutral | info — istemci kendi temasını uygular.</param>
/// <param name="Aciklama">Seçim ekranlarında alt satır (yalnız ödeme yöntemlerinde dolu).</param>
public readonly record struct DurumEtiketi(
    string Kod, string Etiket, string Renk, string Varyant, string? Aciklama = null);

/// <summary>Sipariş/iade akış şeridinin bir adımı.</summary>
public readonly record struct AkisAdimi(string Code, string Label, bool Done, bool Current);

/// <summary>
/// M4 (2026-09-09): durum kodu → etiket/renk TEK kural.
///
/// Neden tek dosya: aynı kodlar üç yerde yazılıyordu — admin panelinde (<c>orderConstants.ts</c>,
/// <c>OrderGrid.StatusLabel</c>), sitenin SSR Hesabım sayfasında (<c>HesabimController</c>) ve
/// mobil istemcide (gömülü sabit). Store uçları ham kod döndürdüğü için mobil kendi listesini
/// tutuyor ve durum eklendiğinde sessizce kodu gösteriyordu.
///
/// ★ İKİ HEDEF KİTLE, İKİ ETİKET SETİ — bilinçli fark:
///  • <see cref="Panel"/>: operasyon dili. <c>pending</c> ile <c>confirmed</c> AYRI görünmek zorunda
///    ("Bekleyen" / "Onaylı") — personel ikisini farklı işler.
///  • <see cref="Vitrin"/>: müşteri dili. Aynı iki kod TEK etikete iner ("Sipariş Alındı") — müşteriyi
///    iç onay adımı ilgilendirmez. Sitenin bugünkü metinleri budur; mobil de bunu alır.
/// Store/mobil yüzeylerinde HER ZAMAN <see cref="Vitrin"/>, panel/Excel yüzeylerinde <see cref="Panel"/>.
///
/// Sürüm (<see cref="Surum"/>) içerikten türetilir: yeni durum eklendiğinde elle bir sayı artırmak
/// gerekmez, <c>GET /api/store/lookups</c>'ın ETag'i kendiliğinden değişir ve istemci önbelleği tazelenir.
/// </summary>
public static class DurumEtiketleri
{
    // ── Varyant → hex. İstemcide tema varsa varyant, yoksa hex kullanılır. ──
    public const string Success = "success", Warning = "warning", Danger = "danger", Neutral = "neutral", Info = "info";

    private static readonly Dictionary<string, string> Renkler = new(StringComparer.Ordinal)
    {
        [Success] = "#16a34a", [Warning] = "#d97706", [Danger] = "#dc2626",
        [Neutral] = "#6b7280", [Info] = "#2563eb",
    };

    private static DurumEtiketi E(string kod, string etiket, string varyant, string? aciklama = null)
        => new(kod, etiket, Renkler[varyant], varyant, aciklama);

    /// <summary>Panel/Excel (operasyon) etiketleri — <c>orderConstants.ts</c> ile birebir aynı.</summary>
    public static class Panel
    {
        public static readonly IReadOnlyList<DurumEtiketi> SiparisDurumu =
        [
            E("pending", "Bekleyen", Warning), E("confirmed", "Onaylı", Warning),
            E("processing", "İşlemde", Warning), E("shipped", "Kargoda", Success),
            E("delivered", "Teslim", Success), E("cancelled", "İptal", Danger),
            // İade akışı planı R5 (2026-09-10): `returned` YALNIZ teslimatsız iadedir — müşteri iadesi
            // sipariş durumunu değiştirmez; eski "İade" etiketi bu yüzden yanıltıcıydı.
            E("returned", "Teslimatsız İade", Danger),
        ];

        public static readonly IReadOnlyList<DurumEtiketi> OdemeDurumu =
        [
            E("pending", "Bekliyor", Warning), E("unpaid", "Ödenmedi", Warning),
            E("paid", "Ödendi", Success), E("partial", "Kısmi", Warning),
            E("underpaid", "Eksik Ödeme", Danger),   // PayTR callback tutarı sipariş tutarından düşük
            E("refunded", "İade Edildi", Neutral), E("failed", "Başarısız", Danger),
        ];

        public static readonly IReadOnlyList<DurumEtiketi> OdemeYontemi =
        [
            E("kart", "Kart (Online)", Info), E("kapida-nakit", "Kapıda Nakit", Neutral),
            E("kapida-kart", "Kapıda Kart", Neutral),
        ];

        public static readonly IReadOnlyList<DurumEtiketi> IadeDurumu =
        [
            E("requested", "Talep Edildi", Warning), E("approved", "Onaylandı", Warning),
            E("received", "Teslim Alındı", Success), E("refunded", "Geri Ödendi", Success),
            // İade planı §2.3: geri ödeme UYGUN DEĞİLSE teslim alma sonrası iade kendiliğinden kapanır
            // ("refunded" yanıltıcı olurdu — para ödenmedi).
            E("closed", "Tamamlandı (geri ödeme yok)", Neutral),
            E("rejected", "Reddedildi", Danger),
        ];

        /// <summary>İade planı R1: iki iade tipi. <c>undelivered</c> = paket müşteriye ulaşmadı / kabul edilmedi
        /// ya da faturalı ama kargosuz; <c>customer</c> = teslim sonrası müşteri iadesi.</summary>
        public static readonly IReadOnlyList<DurumEtiketi> IadeTipi =
        [
            E("undelivered", "Teslimatsız İade", Danger), E("customer", "Müşteri İadesi", Info),
        ];

        /// <summary>İade geri ödeme durumu; <c>not_applicable</c> = para iadesi hesaplanmaz (nedeni
        /// <see cref="GeriOdemeYokNedeni"/>).</summary>
        public static readonly IReadOnlyList<DurumEtiketi> GeriOdemeDurumu =
        [
            E("pending", "Bekliyor", Warning), E("completed", "Tamamlandı", Success),
            E("not_applicable", "Geri ödeme yok", Neutral),
        ];

        /// <summary>Geri ödeme yapılmama nedeni (İade planı R7-R10; <c>Return.RefundNotApplicableReason</c>).</summary>
        public static readonly IReadOnlyList<DurumEtiketi> GeriOdemeYokNedeni =
        [
            E("cod_not_collected", "Kapıda ödeme — tahsilat yapılmadı", Neutral),
            E("marketplace", "Pazaryeri siparişi — iadeyi pazaryeri yapar", Neutral),
            E("unpaid", "Müşteriden tahsilat yok", Neutral),
            E("already_refunded", "Tahsil edilen tutarın tamamı zaten iade edildi", Neutral),
        ];

        public static readonly IReadOnlyList<DurumEtiketi> YorumDurumu =
        [
            E("pending", "Onay Bekliyor", Warning), E("approved", "Onaylı", Success),
            E("rejected", "Reddedildi", Danger),
        ];

        public static readonly IReadOnlyList<DurumEtiketi> SoruDurumu =
        [
            E("pending", "Cevap Bekleyen", Warning), E("answered", "Cevaplanan (yayında)", Success),
            E("hidden", "Yayından Kaldırılan", Neutral),
        ];
    }

    /// <summary>
    /// Vitrin (müşteri) etiketleri — site Hesabım sayfasının bugünkü metinleri; store/mobil uçları bunu döner.
    /// </summary>
    public static class Vitrin
    {
        public static readonly IReadOnlyList<DurumEtiketi> SiparisDurumu =
        [
            // pending + confirmed aynı etiket: müşteri iç onay adımını görmez.
            E("pending", "Sipariş Alındı", Info), E("confirmed", "Sipariş Alındı", Info),
            E("processing", "Hazırlanıyor", Warning), E("shipped", "Kargoda", Info),
            E("delivered", "Teslim Edildi", Success), E("cancelled", "İptal Edildi", Danger),
            // İade planı K7 (2026-09-10): `returned` yalnız teslimatsız iade → müşteriye "Teslim Edilemedi";
            // eski "İade Edildi" müşteri iadesiyle karışıyordu (o, sipariş durumunu değiştirmez).
            E("returned", "Teslim Edilemedi", Neutral),
        ];

        public static readonly IReadOnlyList<DurumEtiketi> OdemeDurumu =
        [
            E("pending", "Ödeme Bekleniyor", Warning), E("unpaid", "Ödeme Alınmadı", Warning),
            E("paid", "Ödeme Alındı", Success), E("partial", "Kısmi Ödeme", Warning),
            E("underpaid", "Eksik Ödeme", Danger),
            E("refunded", "Ödeme İade Edildi", Neutral), E("failed", "Ödeme Başarısız", Danger),
        ];

        public static readonly IReadOnlyList<DurumEtiketi> OdemeYontemi =
        [
            E("kart", "Kredi/Banka Kartı", Info, "Kartınızla güvenli online ödeme"),
            E("kapida-nakit", "Kapıda Nakit Ödeme", Neutral, "Siparişinizi teslim alırken nakit ödeyin"),
            E("kapida-kart", "Kapıda Kart ile Ödeme", Neutral, "Siparişinizi teslim alırken kartla ödeyin"),
        ];

        public static readonly IReadOnlyList<DurumEtiketi> IadeDurumu =
        [
            E("requested", "İade Talebi Alındı", Warning), E("approved", "İade Onaylandı", Warning),
            E("received", "İade İnceleniyor", Warning), E("refunded", "İade Tamamlandı", Success),
            E("closed", "İade Tamamlandı", Success),   // geri ödeme yok — müşteri "tamamlandı" görür
            E("rejected", "İade Reddedildi", Danger),
        ];

        /// <summary>Müşteri dilinde iade tipi: teslimatsız iade müşteriye "Teslim Edilemedi" olarak görünür.</summary>
        public static readonly IReadOnlyList<DurumEtiketi> IadeTipi =
        [
            E("undelivered", "Teslim Edilemedi", Neutral), E("customer", "İade", Info),
        ];

        public static readonly IReadOnlyList<DurumEtiketi> YorumDurumu =
        [
            E("pending", "Onay Bekliyor", Warning), E("approved", "Yayında", Success),
            E("rejected", "Yayınlanmadı", Danger),
        ];

        public static readonly IReadOnlyList<DurumEtiketi> SoruDurumu =
        [
            E("pending", "Cevap Bekleniyor", Warning), E("answered", "Cevaplandı", Success),
            E("hidden", "Yayında Değil", Neutral),
        ];

        /// <summary><c>GET /api/store/lookups</c>'ın döndüğü aile sözlüğü (anahtarlar sözleşmedir).</summary>
        public static readonly IReadOnlyDictionary<string, IReadOnlyList<DurumEtiketi>> Aileler =
            new Dictionary<string, IReadOnlyList<DurumEtiketi>>(StringComparer.Ordinal)
            {
                ["orderStatus"] = SiparisDurumu,
                ["paymentStatus"] = OdemeDurumu,
                ["paymentMethod"] = OdemeYontemi,
                ["returnStatus"] = IadeDurumu,
                ["returnType"] = IadeTipi,          // İade planı (2026-09-10)
                ["reviewStatus"] = YorumDurumu,
                ["questionStatus"] = SoruDurumu,
            };
    }

    // ── Arama yardımcıları: bilinmeyen kod ASLA boş dönmez, ham kodu gösterir ──

    /// <summary>Kodun etiketi; kod aileye yabancıysa kodun kendisi (yeni durum eklenince ekran boş kalmaz).</summary>
    public static string Etiket(IReadOnlyList<DurumEtiketi> aile, string? kod)
        => Bul(aile, kod)?.Etiket ?? kod ?? "";

    /// <summary>Kodun hex rengi; yabancı kodda nötr.</summary>
    public static string Renk(IReadOnlyList<DurumEtiketi> aile, string? kod)
        => Bul(aile, kod)?.Renk ?? Renkler[Neutral];

    /// <summary>Kodun anlamsal varyantı; yabancı kodda <c>neutral</c>.</summary>
    public static string Varyant(IReadOnlyList<DurumEtiketi> aile, string? kod)
        => Bul(aile, kod)?.Varyant ?? Neutral;

    private static DurumEtiketi? Bul(IReadOnlyList<DurumEtiketi> aile, string? kod)
    {
        if (string.IsNullOrWhiteSpace(kod)) return null;
        foreach (var e in aile)
            if (string.Equals(e.Kod, kod, StringComparison.OrdinalIgnoreCase)) return e;
        return null;
    }

    // ── Akış şeridi (timeline) ──

    /// <summary>Sipariş akışının 4 adımı — site Hesabım şeridiyle aynı sıra/etiket.</summary>
    public static readonly IReadOnlyList<(string Kod, string Etiket)> SiparisAkisAdimlari =
        [("alindi", "Sipariş Alındı"), ("hazirlaniyor", "Hazırlanıyor"), ("kargoda", "Kargoda"), ("teslim", "Teslim Edildi")];

    /// <summary>İade akışının 4 adımı.</summary>
    public static readonly IReadOnlyList<(string Kod, string Etiket)> IadeAkisAdimlari =
        [("talep", "İade Talebi Alındı"), ("onay", "İade Onaylandı"), ("inceleme", "İade İnceleniyor"), ("tamamlandi", "İade Tamamlandı")];

    /// <summary>
    /// Sipariş durumunun akış şeridindeki yeri: kaçıncı adımda (1-4) ve akış devam ediyor mu.
    /// <c>cancelled</c> için adım 0 = şerit GÖSTERİLMEZ (tasarımda iptal akışı yok, site de gizler).
    /// </summary>
    private static (int Adim, bool Devam) SiparisAdimi(string? status) => status switch
    {
        "pending" or "confirmed" => (1, true),
        "processing" => (2, true),
        "shipped" => (3, true),
        "delivered" => (4, false),
        "returned" => (4, false),
        "cancelled" => (0, false),
        _ => (1, true),
    };

    private static (int Adim, bool Devam) IadeAdimi(string? status) => status switch
    {
        "requested" => (1, true),
        "approved" => (2, true),
        "received" => (3, true),
        "refunded" => (4, false),
        "closed" => (4, false),     // geri ödeme yok — akış tamamlandı
        "rejected" => (0, false),
        _ => (1, true),
    };

    /// <summary>
    /// Siparişin akış şeridi. İptal edilen siparişte BOŞ liste döner — istemci şeridi çizmez,
    /// durumu <c>statusLabel</c>'dan ("İptal Edildi") gösterir.
    /// </summary>
    public static List<AkisAdimi> SiparisAkisi(string? status)
        => Akis(SiparisAkisAdimlari, SiparisAdimi(status));

    /// <summary>İade akışı bitti mi (para ödendi ya da ödeme gerekmedi) — İade planı §2.3.</summary>
    public static bool IadeTamamlandi(string? returnStatus) => returnStatus is "refunded" or "closed";

    /// <summary>İadenin akış şeridi; reddedilen iadede boş liste.</summary>
    public static List<AkisAdimi> IadeAkisi(string? status)
        => Akis(IadeAkisAdimlari, IadeAdimi(status));

    private static List<AkisAdimi> Akis(IReadOnlyList<(string Kod, string Etiket)> adimlar, (int Adim, bool Devam) yer)
    {
        if (yer.Adim <= 0) return [];
        // Site kuralı: akış bittiyse (delivered/returned/refunded) tüm adımlar tamam, "current" yok.
        var tamamlanan = yer.Devam ? yer.Adim - 1 : adimlar.Count;
        var liste = new List<AkisAdimi>(adimlar.Count);
        for (var i = 0; i < adimlar.Count; i++)
        {
            var done = i < tamamlanan;
            liste.Add(new AkisAdimi(adimlar[i].Kod, adimlar[i].Etiket, done, !done && i == tamamlanan && yer.Devam));
        }
        return liste;
    }

    // ── Müşteri aksiyon bayrakları — komutların dayattığı kuralın TEK kaynağı ──

    /// <summary>Müşteri kendi siparişini iptal edebilir mi (<c>Order.CancellableStatuses</c>).</summary>
    public static bool IptalEdilebilir(string? status) => status is "pending" or "confirmed";

    /// <summary>Sipariş iadeye açık mı (<c>CreateStoreReturnCommandHandler</c>: yalnız teslim edilmiş).</summary>
    public static bool IadeEdilebilir(string? status) => status == "delivered";

    /// <summary>Yorum yazılabilir mi (teslim edilmiş sipariş kalemi şartı). Ürün başına
    /// "zaten yorumladı" denetimi <c>reviews/reviewable</c> ucundadır — bu bayrak sipariş düzeyidir.</summary>
    public static bool YorumYazilabilir(string? status) => status == "delivered";

    // ── Sürüm: içerik hash'i (elle artırma yok) ──

    private static readonly Lazy<string> _surum = new(() =>
    {
        var sb = new StringBuilder();
        foreach (var (ad, aile) in Vitrin.Aileler.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            sb.Append(ad).Append('|');
            foreach (var e in aile)
                sb.Append(e.Kod).Append(':').Append(e.Etiket).Append(':').Append(e.Renk)
                  .Append(':').Append(e.Varyant).Append(':').Append(e.Aciklama).Append(';');
        }
        foreach (var a in SiparisAkisAdimlari) sb.Append(a.Kod).Append(':').Append(a.Etiket).Append(';');
        foreach (var a in IadeAkisAdimlari) sb.Append(a.Kod).Append(':').Append(a.Etiket).Append(';');

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash)[..12].ToLowerInvariant();
    });

    /// <summary>Etiket kataloğunun içerik sürümü — <c>lookups</c> ETag'i ve istemci önbelleği bununla tazelenir.</summary>
    public static string Surum => _surum.Value;
}
