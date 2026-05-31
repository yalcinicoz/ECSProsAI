using System.Text;
using System.Text.RegularExpressions;

namespace ECSPros.Catalog.Application.Helpers;

public static class SlugHelper
{
    private static readonly Dictionary<char, char> TurkishMap = new()
    {
        ['ç'] = 'c', ['Ç'] = 'c',
        ['ğ'] = 'g', ['Ğ'] = 'g',
        ['ı'] = 'i',
        ['İ'] = 'i',
        ['ö'] = 'o', ['Ö'] = 'o',
        ['ş'] = 's', ['Ş'] = 's',
        ['ü'] = 'u', ['Ü'] = 'u',
    };

    public static string ToSnakeCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
            sb.Append(TurkishMap.TryGetValue(c, out var mapped) ? mapped : c);

        var result = sb.ToString().ToLowerInvariant();
        result = Regex.Replace(result, @"[^a-z0-9]+", "_");
        result = result.Trim('_');
        result = Regex.Replace(result, @"_+", "_");

        return result;
    }

    /// <summary>
    /// nameI18n içinden Türkçe adı ("tr") ya da ilk mevcut dili alarak snake_case kod üretir.
    /// </summary>
    public static string FromNameI18n(Dictionary<string, string> nameI18n)
    {
        if (nameI18n.TryGetValue("tr", out var tr) && !string.IsNullOrWhiteSpace(tr))
            return ToSnakeCase(tr);

        var first = nameI18n.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        return first is null ? string.Empty : ToSnakeCase(first);
    }
}
