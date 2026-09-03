using System.Text.RegularExpressions;

namespace ECSPros.Api.Services.LegacyImport;

public sealed record LegacyInvoiceNumber(string Serial, string Year, int Sequence);

public static partial class LegacyInvoiceNumberParser
{
    [GeneratedRegex("^(?<serial>[A-Za-z0-9]{3})(?<year>[0-9]{4})(?<sequence>[0-9]{9})$",
        RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    public static LegacyInvoiceNumber? Parse(string? value)
    {
        var match = Pattern().Match(value?.Trim() ?? string.Empty);
        if (!match.Success || !int.TryParse(match.Groups["sequence"].Value, out var sequence)) return null;
        return new(
            match.Groups["serial"].Value.ToUpperInvariant(),
            match.Groups["year"].Value,
            sequence);
    }
}
