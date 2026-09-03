using System.Globalization;
using System.Text;

namespace ECSPros.Api.Services.LegacyImport;

public sealed record LegacyOrderStatusMapping(string Status, int Rank);

/// <summary>Legacy sipariş durumlarını tek ve tahminsiz hedef sözlüğüne dönüştürür.</summary>
public static class LegacyOrderStatusMapper
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static LegacyOrderStatusMapping? Map(string? rawStatus)
    {
        var key = Key(rawStatus);
        if (key.Contains("İADE", StringComparison.Ordinal))
            return new("returned", 9);

        return key switch
        {
            "ONAY BEKLİYOR" or "BEKLEMEDE" => new("pending", 0),
            "ONAYLANDI" => new("confirmed", 1),
            "HAZIRLANIYOR" or "FATURASI KESİLDİ" => new("processing", 2),
            "KARGOYA VERİLDİ" => new("shipped", 3),
            "TESLİM EDİLDİ" => new("delivered", 4),
            "İPTAL EDİLDİ" => new("cancelled", 9),
            _ => null
        };
    }

    private static string Key(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized = value.Trim().Normalize(NormalizationForm.FormKC).ToUpper(Turkish);
        return string.Join(' ', normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
