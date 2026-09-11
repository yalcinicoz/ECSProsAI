using System.Text.Json;
using System.Text.RegularExpressions;

namespace ECSPros.Catalog.Application.Helpers;

/// <summary>Matches dictionary VALUES in all languages; bound JSONPath, never interpolated SQL.</summary>
public static class ProductGroupSearch
{
    public static string Pattern(string value)
    {
        var words = Regex.Split(value.Trim().ToLowerInvariant(), @"\s+");
        return string.Join(@"\s+", words.Select(word => string.Concat(word.Select(c => c switch
        {
            'c' or 'ç' => "[cçÇC]", 'g' or 'ğ' => "[gğĞG]",
            'i' or 'ı' or 'İ' => "[iıİI]", 'o' or 'ö' => "[oöÖO]",
            's' or 'ş' => "[sşŞS]", 'u' or 'ü' => "[uüÜU]",
            _ => Regex.Escape(c.ToString())
        }))));
    }

    public static string PathFor(string term) => "$.* ? (@ like_regex " + JsonSerializer.Serialize(Pattern(term)) + " flag \"i\")";

    // Mapped to PostgreSQL jsonb_path_exists by CatalogDbContext.
    public static bool Matches(Dictionary<string, string> values, string path)
    {
        const string prefix = "$.* ? (@ like_regex ";
        const string suffix = " flag \"i\")";
        var pattern = JsonSerializer.Deserialize<string>(path[prefix.Length..^suffix.Length])!;
        return values.Values.Any(v => Regex.IsMatch(v, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
    }
}
