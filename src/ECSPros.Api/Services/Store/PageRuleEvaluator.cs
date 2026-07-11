using System.Text.Json;

namespace ECSPros.Api.Services.Store;

/// <summary>
/// G10 kural motoru: RuleJson'ı ziyaretçi segmentiyle (G9) eşler. Spec kuralları:
/// kural yoksa herkese göster; alan içi çoklu değer OR, alanlar arası AND; boş/eksik
/// alan değerlendirilmez; uymayan ziyaretçiye içerik gösterilmez ve otomatik default
/// aranmaz. Segmentte bilinmeyen boyut (şehir yok, cinsiyet unknown...) o boyutu
/// kısıtlayan hiçbir kuralla eşleşmez. Şema doğrulaması yayın öncesinde yapılır
/// (PageBlockCatalog.ValidateRule) — buraya bozuk JSON gelirse içerik GİZLENİR
/// (hedefli içeriği herkese sızdırmaktansa basmamak güvenli taraf).
/// </summary>
public static class PageRuleEvaluator
{
    public static bool Matches(string? ruleJson, VisitorSegment segment)
    {
        if (string.IsNullOrWhiteSpace(ruleJson)) return true;

        JsonDocument belge;
        try { belge = JsonDocument.Parse(ruleJson); }
        catch { return false; }

        using (belge)
        {
            if (belge.RootElement.ValueKind != JsonValueKind.Object) return false;

            foreach (var alan in belge.RootElement.EnumerateObject())
            {
                if (alan.Value.ValueKind != JsonValueKind.Array) return false;
                var degerler = alan.Value.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString()!)
                    .ToList();
                if (degerler.Count == 0) continue; // boş alan değerlendirilmez (spec)

                var ziyaretci = alan.Name switch
                {
                    "city" => segment.CityCode,
                    "region" => segment.Region,
                    "gender" => segment.Gender is "male" or "female" ? segment.Gender : null,
                    "device" => segment.Device,
                    "membership" => segment.IsMember ? "member" : "guest",
                    "memberGroup" => segment.MemberGroupId?.ToString(),
                    _ => null, // bilinmeyen alan (ileri sürüm kuralı) — eşleşmez, içerik gizlenir
                };
                if (ziyaretci is null
                    || !degerler.Any(d => string.Equals(d, ziyaretci, StringComparison.OrdinalIgnoreCase)))
                    return false; // alanlar arası AND: tek alan tutmazsa kural tutmaz
            }
        }
        return true;
    }
}
