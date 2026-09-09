namespace ECSPros.Shared.Contracts;

/// <summary>
/// M3 (2026-09-09): varyantın insan-okur seçenek metni — "Renk: Krem, Beden: S".
///
/// Neden tek kural: aynı metin sepet satırında (<c>optionsText</c>), sipariş/yorum uçlarında ve
/// ürün detayında (<c>variants[].variantInfo</c>) görünüyor. Ayrı ayrı kurulduğunda aynı varyant
/// iki ekranda farklı yazılıyordu.
///
/// Kurallar:
///  • <c>filtre_rengi</c> DIŞARIDA — vitrin filtresi için tutulan iç eksendir ve "Renk"i tekrar eder
///    ("Beden: 44, Filtre Rengi: Lacivert, Renk: Lacivert" gibi metinler üretiyordu).
///  • Sıra: önce renk, sonra beden, sonra kalanlar (kod sırasına göre) — müşterinin okuduğu sıra.
///  • En çok 3 seçenek: kart/sepet satırı tek satırda kalmalı.
/// </summary>
public static class VaryantSecenekMetni
{
    private static readonly string[] Gizli = ["filtre_rengi"];

    private static int Oncelik(string kod) => kod switch
    {
        "renk" => 0,
        "beden" => 1,
        _ => 2,
    };

    /// <param name="secenekler">(tip kodu, tip adı, değer adı) üçlüleri.</param>
    public static string? Kur(IEnumerable<(string Kod, string TipAd, string DegerAd)> secenekler)
    {
        var metin = string.Join(", ", secenekler
            .Where(s => !Gizli.Contains(s.Kod, StringComparer.OrdinalIgnoreCase))
            .Where(s => !string.IsNullOrWhiteSpace(s.DegerAd))
            .OrderBy(s => Oncelik(s.Kod))
            .ThenBy(s => s.Kod, StringComparer.Ordinal)
            .Take(3)
            .Select(s => $"{s.TipAd}: {s.DegerAd}"));

        return string.IsNullOrWhiteSpace(metin) ? null : metin;
    }
}
