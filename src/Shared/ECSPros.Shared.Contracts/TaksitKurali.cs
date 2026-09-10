namespace ECSPros.Shared.Contracts;

/// <summary>Kanal taksit tablosunun bir satırı: taksit sayısı → müşteriye yansıtılan vade farkı (%).</summary>
public sealed record TaksitTablosuSatiri(int Adet, decimal VadeFarkiYuzde);

/// <summary>Müşteriye sunulan taksit seçeneği. Tek çekim: Adet=1, VadeFarki=0.</summary>
public sealed record TaksitSecenegiSonucu(int Adet, decimal Birim, decimal Toplam, decimal VadeFarki);

/// <summary>Vade farkının KDV oranına göre dağıtılmış satırı (faturaya yazılan biçim).</summary>
public sealed record VadeFarkiKdvSatiri(decimal KdvOrani, decimal Brut, decimal Net, decimal Kdv);

/// <summary>
/// Taksitlendirmenin MÜŞTERİ tarafı (2026-09-10 kullanıcı kararları). Ödeme aracısıyla (PayTR) aramızdaki
/// komisyon bu kuralın konusu değildir; müşteriye yansıtılan vade farkı kanalın kendi tablosundan gelir:
///  • Tablo HER ZAMAN kanal bazlıdır (kart programı/banka bazlı değil).
///  • Yalnız site ve mobil kart ödemelerinde uygulanır (POS ve kapıda kart kapsam dışı).
///  • Vade farkı faturada ürünlere yedirilmez; ürünlerin KDV oranlarına göre ORANLANIR ve oran başına
///    ayrı satır yazılır (örn. %10'luk ve %20'lik ürünler varsa iki "Vade Farkı" satırı).
///  • İadede müşteriye ödenen tutar = iade edilen ürün tutarı + o ürünün vade farkı payı.
/// Kural TEK yerde durur: ödeme sayfası (web/mobil), ödeme başlatma, fatura ve iade aynı hesabı kullanır.
/// </summary>
public static class TaksitKurali
{
    public const int EnAzTaksit = 2;
    public const int EnCokTaksit = 12;

    /// <summary>Kaynak seçimi: ödeme aracısının tablosu (bugünkü davranış) ya da kanalın kendi tablosu.</summary>
    public const string KaynakOdemeAracisi = "provider";
    public const string KaynakKendiTablomuz = "own";

    /// <summary>Baz tutar için müşteri seçenekleri. Tek çekim HER ZAMAN ilk satırdır; tablo satırları
    /// 2..12 aralığında, negatif olmayan yüzdeyle ve aynı adette bir kez sayılır (son yazılan kazanır).
    /// Toplam = baz × (1 + %/100) kuruşa yuvarlanır; aylık = toplam / adet (salt gösterim — tahsilat toplamdır).</summary>
    public static List<TaksitSecenegiSonucu> Secenekler(decimal baz, IEnumerable<TaksitTablosuSatiri>? tablo)
    {
        var sonuc = new List<TaksitSecenegiSonucu> { new(1, baz, baz, 0m) };
        if (baz <= 0 || tablo is null) return sonuc;
        var satirlar = new Dictionary<int, decimal>();
        foreach (var s in tablo)
        {
            if (s.Adet < EnAzTaksit || s.Adet > EnCokTaksit || s.VadeFarkiYuzde < 0) continue;
            satirlar[s.Adet] = s.VadeFarkiYuzde;
        }
        foreach (var (adet, yuzde) in satirlar.OrderBy(k => k.Key))
        {
            var toplam = Yuvarla(baz * (1 + yuzde / 100m));
            sonuc.Add(new TaksitSecenegiSonucu(adet, Yuvarla(toplam / adet), toplam, toplam - baz));
        }
        return sonuc;
    }

    /// <summary>Seçilen taksit için vade farkı (TL). Tabloda olmayan adet → null (seçenek sunulmamıştır, reddedilir).</summary>
    public static decimal? VadeFarki(decimal baz, int adet, IEnumerable<TaksitTablosuSatiri>? tablo)
    {
        if (adet <= 1) return 0m;
        var secenek = Secenekler(baz, tablo).FirstOrDefault(s => s.Adet == adet);
        return secenek?.VadeFarki;
    }

    /// <summary>Vade farkını kalemlere, kalem net tutarı oranında dağıtır (kuruş farkı en büyük kalana).
    /// Toplam sıfır ya da vade farkı sıfırsa her kalem 0 alır. Dönen sözlük kalem anahtarı → pay.</summary>
    public static Dictionary<TKey, decimal> KalemPaylari<TKey>(decimal vadeFarki, IEnumerable<(TKey Anahtar, decimal Tutar)> kalemler)
        where TKey : notnull
    {
        var liste = kalemler.ToList();
        var sonuc = liste.ToDictionary(k => k.Anahtar, _ => 0m);
        var toplam = liste.Sum(k => k.Tutar);
        if (vadeFarki <= 0 || toplam <= 0) return sonuc;
        var kalanlar = new List<(TKey Anahtar, decimal Kesir)>();
        decimal dagitilan = 0m;
        foreach (var (anahtar, tutar) in liste)
        {
            if (tutar <= 0) continue;
            var ham = vadeFarki * tutar / toplam;
            var pay = Math.Floor(ham * 100m) / 100m;
            sonuc[anahtar] = pay; dagitilan += pay;
            kalanlar.Add((anahtar, ham - pay));
        }
        var artik = (int)Math.Round((vadeFarki - dagitilan) * 100m, MidpointRounding.AwayFromZero);
        foreach (var (anahtar, _) in kalanlar.OrderByDescending(k => k.Kesir))
        {
            if (artik <= 0) break;
            sonuc[anahtar] += 0.01m; artik--;
        }
        return sonuc;
    }

    /// <summary>Vade farkını ürünlerin KDV oranlarına göre oranlar: her oran grubunun payı o gruptaki kalem
    /// tutarlarının toplam içindeki oranıdır; pay KDV DAHİL brüttür, net = brüt / (1 + oran/100).
    /// Sonuç orana göre artan sırada; boş tutar/oran → boş liste.</summary>
    public static List<VadeFarkiKdvSatiri> KdvSatirlari(decimal vadeFarki, IEnumerable<(decimal Tutar, decimal KdvOrani)> kalemler)
    {
        var gruplar = kalemler.Where(k => k.Tutar > 0)
            .GroupBy(k => k.KdvOrani)
            .Select(g => (Oran: g.Key, Tutar: g.Sum(x => x.Tutar)))
            .OrderBy(g => g.Oran)
            .ToList();
        if (vadeFarki <= 0 || gruplar.Count == 0) return [];
        var paylar = KalemPaylari(vadeFarki, gruplar.Select(g => (g.Oran, g.Tutar)));
        return gruplar.Select(g =>
        {
            var brut = paylar[g.Oran];
            var net = Yuvarla(brut / (1 + g.Oran / 100m));
            return new VadeFarkiKdvSatiri(g.Oran, brut, net, brut - net);
        }).ToList();
    }

    private static decimal Yuvarla(decimal d) => Math.Round(d, 2, MidpointRounding.AwayFromZero);
}
