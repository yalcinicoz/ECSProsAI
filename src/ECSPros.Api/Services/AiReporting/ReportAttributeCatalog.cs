using ECSPros.Catalog.Application.Services;
using Microsoft.EntityFrameworkCore;
using ECSPros.Shared.Kernel.Authorization;

namespace ECSPros.Api.Services.AiReporting;

public sealed class ReportCatalogException(string message) : Exception(message);

public interface IReportAttributeCatalog
{
    Task<IReadOnlyList<ReportField>> LoadAsync(IReadOnlySet<string> permissions, CancellationToken ct);
    Task<IReadOnlyList<ReportField>> LoadForPromptAsync(IReadOnlySet<string> permissions, string prompt, CancellationToken ct)
        => LoadAsync(permissions, ct);
    Task<string?> CheckBindingsAsync(DynamicReportPlan plan, IReadOnlySet<string> permissions, CancellationToken ct)
        => Task.FromResult<string?>(null);
}

/// <summary>Read current controlled catalog definitions, never product/customer rows or custom text.</summary>
public sealed class ReportAttributeCatalog(ICatalogDbContext catalog) : IReportAttributeCatalog
{
    public async Task<string?> CheckBindingsAsync(DynamicReportPlan plan, IReadOnlySet<string> permissions, CancellationToken ct)
    {
        var requested = ReportBindingGuard.ProductGroupValues(plan);
        if (requested.Count == 0) return null;
        // Same permissions as the source; never inspect hidden catalog dictionaries.
        var fields = await LoadForPromptAsync(permissions, string.Join(" ", requested), ct);
        if (fields.Count == 0) return null;
        var candidates = requested.Select(ReportBindingGuard.Fold).Distinct().ToArray();
        var realGroups = await catalog.ProductGroups.AsNoTracking().Where(g => !g.IsDeleted)
            .Select(g => ECSPros.Shared.Kernel.Grid.GridJson.Text(g.NameI18n, "tr"))
            .Where(label => candidates.Contains(label.Replace("İ", "i").ToLower().Replace("ı", "i").Replace("ö", "o")
                .Replace("ü", "u").Replace("ş", "s").Replace("ç", "c").Replace("ğ", "g")))
            .Distinct().Take(129).ToListAsync(ct);
        if (realGroups.Count > 128) throw new ReportCatalogException("Eşleşen grup sözlüğü sınırı aşıldı.");
        return ReportBindingGuard.Question(requested, realGroups, fields);
    }
    public async Task<IReadOnlyList<ReportField>> LoadForPromptAsync(IReadOnlySet<string> permissions, string prompt, CancellationToken ct)
    {
        var fields = await LoadAsync(permissions, ct);
        if (fields.Count == 0) return fields;
        var ids = fields.Select(f => Guid.Parse(f.Id[10..])).ToArray();
        static string Fold(string text) => string.Concat(text.ToLowerInvariant().Replace('ı', 'i')
            .Normalize(System.Text.NormalizationForm.FormD).Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)).ToLowerInvariant();
        var words = System.Text.RegularExpressions.Regex.Matches(Fold(prompt), @"[\p{L}\p{N}]+").Select(m => m.Value).ToArray();
        var phrases = new HashSet<string>();
        for (var i = 0; i < words.Length; i++)
            for (var length = 1; length <= 4 && i + length <= words.Length; length++)
            {
                var phrase = string.Join(" ", words.Skip(i).Take(length));
                if (phrase.Length is > 1 and <= 128 && phrase.Any(char.IsLetter)) phrases.Add(phrase);
            }
        if (phrases.Count > 4096) throw new ReportCatalogException("İstek seçenek araması sınırı aşıldı.");
        var candidates = phrases.ToArray();
        // Filter on the server; never load the full (potentially huge) option dictionary.
        var values = await catalog.AttributeValues.AsNoTracking()
            .Where(v => !v.IsDeleted && v.IsActive && ids.Contains(v.AttributeTypeId))
            .Select(v => new { v.AttributeTypeId, Label = ECSPros.Shared.Kernel.Grid.GridJson.Text(v.NameI18n, "tr") })
            .Where(v => candidates.Contains(v.Label.Replace("İ", "i").ToLower().Replace("ı", "i").Replace("ö", "o")
                .Replace("ü", "u").Replace("ş", "s").Replace("ç", "c").Replace("ğ", "g")))
            .Distinct().OrderBy(v => v.AttributeTypeId).ThenBy(v => v.Label).Take(257).ToListAsync(ct);
        if (values.Count > 256) throw new ReportCatalogException("İstek çok fazla özellik seçeneğiyle eşleşiyor; alan adıyla daraltın.");
        var matches = values.Where(v => v.Label is { Length: > 1 and <= 128 } && !v.Label.Any(char.IsControl))
            .GroupBy(v => v.AttributeTypeId).ToDictionary(g => g.Key, g => g.Select(v => v.Label!).Distinct().Take(20).ToArray());
        return fields.Select(f => matches.TryGetValue(Guid.Parse(f.Id[10..]), out var labels)
            ? f with { MatchedValues = labels, Description = f.Description + " İstek metninde bu özelliğe ait eşleşen gerçek seçenekler: " + System.Text.Json.JsonSerializer.Serialize(labels) }
            : f).ToArray();
    }
    public async Task<IReadOnlyList<ReportField>> LoadAsync(IReadOnlySet<string> permissions, CancellationToken ct)
    {
        if (!permissions.Contains(ReportDictionary.UsePermission)
            || !permissions.Contains(Permissions.InventoryView)
            || !permissions.Contains(Permissions.CatalogProductsView)) return Array.Empty<ReportField>();
        var types = await catalog.AttributeTypes.AsNoTracking()
            .Where(t => !t.IsDeleted && t.IsActive && (t.DataType == "select" || t.DataType == "multi_select"))
            .OrderBy(t => t.Id).Select(t => new { t.Id, t.Code, t.NameI18n }).Take(257).ToListAsync(ct);
        // Do not silently omit definitions, or produce unbounded model schemas.
        if (types.Count > 256) throw new ReportCatalogException("Rapor özellik sözlüğü sınırı aşıldı.");
        if (types.Any(t => t.Code.Length > 128 || (t.NameI18n.GetValueOrDefault("tr")?.Length ?? 0) > 128))
            throw new ReportCatalogException("Rapor özellik adı sınırı aşıldı.");
        return types.Select(t => Field(t.Id,
            t.NameI18n.GetValueOrDefault("tr") ?? t.Code, t.Code)).ToArray();
    }

    public static ReportField Field(Guid id, string label, string code) => new(
        "attribute." + id.ToString("D"), label, "dimension", Permissions.CatalogProductsView,
        $"Ürün/varyant özelliği ({code}). Gruplama ve eq/in ad filtresi. Filtreye istenen seçenek adını yazın. "
        + "Varyant değeri öncelikli, yoksa ürün değeri. Birden çok değer birleşik bir gruptur; eksik değer Belirtilmemiş. ",
        Array.AsReadOnly(new[] { "eq", "in" }));

    public static bool TryId(string id, out Guid typeId)
    {
        typeId = Guid.Empty;
        return id.StartsWith("attribute.", StringComparison.Ordinal)
            && Guid.TryParseExact(id[10..], "D", out typeId) && typeId != Guid.Empty;
    }
}
