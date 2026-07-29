using System.Globalization;
using System.Text;

namespace ECSPros.Api.Services.Marketplace.Mapping;

/// <summary>
/// Eşleme öneri katmanının benzerlik ölçümü (§2.3 öneri katmanı). Türkçe'ye göre
/// normalize eder (ı/ş/ğ/ü/ö/ç katlama), token kesişimi + kapsama bonusuyla 0-100
/// skor üretir. İki ayrı DB'deki adlar (ana DB grup/değer ↔ referans DB kategori/değer)
/// uygulama içinde karşılaştırılır — cross-DB extension gerekmez.
/// </summary>
public static class TextSimilarity
{
    public static string Normalize(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var ch in input.ToLower(new CultureInfo("tr-TR")))
            sb.Append(ch switch
            {
                'ı' => 'i', 'ş' => 's', 'ğ' => 'g', 'ü' => 'u', 'ö' => 'o', 'ç' => 'c',
                _ => ch
            });
        return sb.ToString();
    }

    /// <summary>İki token aynı mı — Türkçe çekim ekleri için önek toleranslı
    /// (ör. "ayakkabi" ↔ "ayakkabisi" eşleşir; kök en az 4 harf).</summary>
    private static bool TokenMatch(string a, string b) =>
        a == b || (a.Length >= 4 && b.Length >= 4 && (a.StartsWith(b) || b.StartsWith(a)));

    /// <summary>0-100 skor: önek-toleranslı token Jaccard (60p) + tam/kapsama bonusu (40p).</summary>
    public static int Score(string a, string b)
    {
        var na = Normalize(a).Trim();
        var nb = Normalize(b).Trim();
        if (na.Length == 0 || nb.Length == 0) return 0;
        if (na == nb) return 100;

        var ta = na.Split(' ', '-', '/', '&', ',').Where(t => t.Length > 1).Distinct().ToList();
        var tb = nb.Split(' ', '-', '/', '&', ',').Where(t => t.Length > 1).Distinct().ToList();
        if (ta.Count == 0 || tb.Count == 0) return 0;

        var usedB = new bool[tb.Count];
        var matched = 0;
        foreach (var t in ta)
            for (var j = 0; j < tb.Count; j++)
                if (!usedB[j] && TokenMatch(t, tb[j])) { usedB[j] = true; matched++; break; }

        double jaccard = (double)matched / (ta.Count + tb.Count - matched);
        double contain = na.Contains(nb) || nb.Contains(na) ? 1.0
            : matched > 0 && matched == Math.Min(ta.Count, tb.Count) ? 0.8
            : 0;

        return (int)Math.Round(jaccard * 60 + contain * 40);
    }

    /// <summary>Adaylar içinden en iyi N eşleşme (minScore altı elenir).</summary>
    public static List<(T Item, int Score)> TopMatches<T>(
        string query, IEnumerable<T> candidates, Func<T, string> nameOf, int take, int minScore = 35)
        => candidates
            .Select(c => (Item: c, Score: Score(query, nameOf(c))))
            .Where(x => x.Score >= minScore)
            .OrderByDescending(x => x.Score)
            .Take(take)
            .ToList();
}
