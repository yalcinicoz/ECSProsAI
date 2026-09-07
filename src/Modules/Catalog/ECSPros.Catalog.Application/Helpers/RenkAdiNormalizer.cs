using System.Globalization;
using System.Text.RegularExpressions;

namespace ECSPros.Catalog.Application.Helpers;

/// <summary>
/// A2 (2026-09-07, mobil): 'renk' değer havuzundaki (ERP kaynaklı serbest metin) bitişik/kısaltmalı/büyük harfli
/// renk adlarını TEK yazıma çeker: "Siyahbeyaz"→"Siyah Beyaz", "a.füme"→"Açık Füme", "KİREMİT MÜRDÜM"→"Kiremit Mürdüm",
/// "kremsiyahlı"→"Krem Siyahlı", "pempe"→"Pembe". Deterministiktir: sözlük havuzun kendisinden kurulur
/// (boşluklu adlardaki ≥2 kez geçen sözcükler + tek başına da geçen sözcükler) + sabit ek sözcükler; bitişik ad
/// yalnız 2-3 bilinen sözcüğe TEK biçimde ayrılabiliyorsa değişir, birden çok ayrım varsa dokunulmaz.
/// Çıktı sabit noktadır (normalize(normalize(x)) == normalize(x)) → seed'de idempotent.
/// </summary>
public sealed class RenkAdiNormalizer
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly string[] Ekler = ["lı", "li", "lu", "lü"];

    private static readonly Dictionary<string, string> EkSozluk = new(StringComparer.Ordinal)
    {
        ["mavisi"] = "Mavisi", ["yeşili"] = "Yeşili", ["grisi"] = "Grisi", ["kahvesi"] = "Kahvesi", ["kırık"] = "Kırık",
        ["kemik"] = "Kemik", ["mat"] = "Mat", ["parlak"] = "Parlak", ["saks"] = "Saks", ["bebe"] = "Bebe", ["petrol"] = "Petrol",
        ["kot"] = "Kot", ["oranj"] = "Oranj", ["fuşya"] = "Fuşya", ["pembe"] = "Pembe", ["haki"] = "Haki", ["antrasit"] = "Antrasit",
        ["taba"] = "Taba", ["indigo"] = "İndigo", ["gül"] = "Gül", ["soğan"] = "Soğan", ["kabuğu"] = "Kabuğu", ["parlament"] = "Parlament",
        ["çağla"] = "Çağla", ["menekşe"] = "Menekşe", ["kivi"] = "Kivi", ["aqua"] = "Aqua", ["gümüş"] = "Gümüş", ["bronz"] = "Bronz",
        ["turkuaz"] = "Turkuaz", ["mürdüm"] = "Mürdüm", ["vizon"] = "Vizon", ["lila"] = "Lila", ["mint"] = "Mint", ["hardal"] = "Hardal",
        ["somon"] = "Somon", ["şampanya"] = "Şampanya", ["ekru"] = "Ekru", ["kiremit"] = "Kiremit", ["füme"] = "Füme",
    };

    // Yazım düzeltmeleri (küçük harf, alt dizi): yalnız sık ERP yazım hataları.
    private static readonly (string Yanlis, string Dogru)[] Yazim =
    [
        ("pempe", "pembe"), ("hakı", "haki"), ("antrasıt", "antrasit"), ("tabaa", "taba"), ("fujya", "fuşya"),
        ("saxmavi", "saksmavi"), ("sakmavi", "saksmavi"), ("sax", "saks"), ("lacıvert", "lacivert"),
        ("kırmızi", "kırmızı"), ("kirmizi", "kırmızı"), ("beyas", "beyaz"), ("siyaj", "siyah"), ("yesil", "yeşil"),
        ("fume", "füme"), ("kahverengı", "kahverengi"),
    ];

    private readonly HashSet<string> _sozluk;
    private readonly Dictionary<string, string> _yazimBicimi; // küçük → kanonik yazım
    private readonly Dictionary<string, int> _siklik;

    public static string Kucult(string s) => s.Replace('I', 'ı').Replace('İ', 'i').ToLower(Tr);

    /// <summary>Havuzdaki tüm adlardan sözlük kurar.</summary>
    public RenkAdiNormalizer(IEnumerable<string> havuz)
    {
        var adlar = havuz.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).ToList();
        var bosluklu = adlar.Where(a => a.Contains(' ')).ToList();
        var tekil = adlar.Where(a => !a.Contains(' ')).ToList();

        var tokSiklik = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var a in bosluklu)
            foreach (var t in Regex.Split(a, @"[\s/\-]+"))
                if (t.Length >= 3 && t.All(char.IsLetter))
                    tokSiklik[Kucult(t)] = tokSiklik.GetValueOrDefault(Kucult(t)) + 1;

        var tekilSiklik = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var a in tekil) tekilSiklik[Kucult(a)] = tekilSiklik.GetValueOrDefault(Kucult(a)) + 1;

        _sozluk = new HashSet<string>(tokSiklik.Where(kv => kv.Value >= 2).Select(kv => kv.Key), StringComparer.Ordinal);
        foreach (var k in tekilSiklik.Keys) if (tokSiklik.ContainsKey(k)) _sozluk.Add(k);
        foreach (var k in EkSozluk.Keys) _sozluk.Add(k);

        _siklik = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var kv in tokSiklik) _siklik[kv.Key] = kv.Value;
        foreach (var kv in tekilSiklik) _siklik[kv.Key] = _siklik.GetValueOrDefault(kv.Key) + kv.Value;

        // Kanonik yazım: sözlük sözcüğünün havuzda EN SIK görülen orijinal yazımı (eşitlikte sıralı-küçük olan)
        var yazimSayac = new Dictionary<(string, string), int>();
        foreach (var a in bosluklu)
            foreach (var t in Regex.Split(a, @"[\s/\-]+"))
                if (_sozluk.Contains(Kucult(t))) yazimSayac[(Kucult(t), t)] = yazimSayac.GetValueOrDefault((Kucult(t), t)) + 1;
        foreach (var a in tekil)
            if (_sozluk.Contains(Kucult(a))) yazimSayac[(Kucult(a), a)] = yazimSayac.GetValueOrDefault((Kucult(a), a)) + 1;
        _yazimBicimi = new Dictionary<string, string>(EkSozluk, StringComparer.Ordinal);
        foreach (var kv in yazimSayac.OrderByDescending(x => x.Value).ThenBy(x => x.Key.Item2, StringComparer.Ordinal))
            _yazimBicimi.TryAdd(kv.Key.Item1, kv.Key.Item2);
    }

    private bool Bilinen(string w) =>
        _sozluk.Contains(w) || (w.Length >= 5 && Ekler.Any(e => w.EndsWith(e, StringComparison.Ordinal)) && _sozluk.Contains(w[..^2]));

    private string Basla(string w)
    {
        var c = _yazimBicimi.GetValueOrDefault(w, w);
        if (c.All(ch => !char.IsLetter(ch) || char.IsLower(ch)))
            c = (c[0] == 'i' ? "İ" : c[..1].ToUpper(Tr)) + c[1..];
        return c;
    }

    private List<List<string>> Ayir(string s, int derinlik = 0)
    {
        if (s.Length == 0) return [[]];
        var sonuc = new List<List<string>>();
        for (var i = 3; i < s.Length - 2; i++)
        {
            var w = s[..i];
            if (!Bilinen(w)) continue;
            foreach (var kalan in Ayir(s[i..], derinlik + 1)) sonuc.Add([w, .. kalan]);
        }
        if (derinlik > 0 && Bilinen(s)) sonuc.Add([s]);
        return sonuc;
    }

    private static string OnIsle(string k)
    {
        foreach (var (y, d) in Yazim) k = k.Replace(y, d, StringComparison.Ordinal);
        k = Regex.Replace(k, @"^a\.\s*", "açık ");
        k = Regex.Replace(k, @"^k\.\s*", "koyu ");
        k = Regex.Replace(k, @"([\s/])a\.\s*", "$1açık ");
        k = Regex.Replace(k, @"([\s/])k\.\s*", "$1koyu ");
        k = Regex.Replace(k, @"[\.\-]+", " ");
        return Regex.Replace(k, @"\s+", " ").Trim();
    }

    /// <summary>Normalize edilmiş ad; çözülemiyorsa (bilinmeyen sözcük / belirsiz ayrım) null.</summary>
    public string? Normalize(string ad)
    {
        if (string.IsNullOrWhiteSpace(ad)) return null;
        var parcalar = OnIsle(Kucult(ad.Trim())).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cikti = new List<string>();
        foreach (var p in parcalar)
        {
            if (Bilinen(p)) { cikti.Add(Basla(p)); continue; }
            if (p.Length < 7) return null;
            var ayrimlar = Ayir(p).Where(x => x.Count is >= 2 and <= 3)
                .OrderBy(x => x.Count)
                .ThenByDescending(x => x.Min(w => _siklik.GetValueOrDefault(w)))
                .ToList();
            if (ayrimlar.Count == 0) return null;
            if (ayrimlar.Count > 1 && ayrimlar[1].Count == ayrimlar[0].Count) return null; // belirsiz — dokunma
            cikti.AddRange(ayrimlar[0].Select(Basla));
        }
        return string.Join(' ', cikti);
    }
}
