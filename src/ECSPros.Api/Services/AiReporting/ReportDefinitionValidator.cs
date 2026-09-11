using System.Text;
using System.Text.Json;

namespace ECSPros.Api.Services.AiReporting;

public sealed record ReportValidationResult(ReportDefinition? Definition, string? Error)
{
    public bool IsValid => Definition is not null && Error is null;
}

/// <summary>
/// Pure contract validation only. Stock uses the approved shared inventory permission model;
/// sales will require channel scope. No scope may be supplied by the model or recipe.
/// </summary>
public static class ReportDefinitionValidator
{
    public const int MaxPayloadBytes = 16_384;
    public const int MaxRows = 1_000;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        MaxDepth = 8
    };

    public static ReportValidationResult Parse(string? json, IReadOnlySet<string> permissions,
        bool forExport = false, IReadOnlyList<ReportField>? attributes = null)
    {
        // These are current SERVER permissions; never deserialize them from the recipe/model.
        if (!permissions.Contains(ReportDictionary.UsePermission)
            || (forExport && !permissions.Contains(ReportDictionary.ExportPermission)))
            return Fail("Bu rapor işlemi için yetkiniz yok.");
        if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaxPayloadBytes)
            return Fail("Rapor tarifi boş veya izin verilen boyutu aşıyor.");

        ReportDefinition? recipe;
        try
        {
            // System.Text.Json otherwise silently accepts duplicate property names.
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            if (HasDuplicateProperties(document.RootElement))
                return Fail("Rapor tarifinde yinelenen alan var.");
            recipe = JsonSerializer.Deserialize<ReportDefinition>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return Fail("Rapor tarifi geçerli biçimde değil.");
        }

        if (recipe?.Subject == OrderReportRecipe.Subject)
        {
            try { OrderReportRecipe.Parse(recipe, permissions); return new(recipe, null); }
            catch (Exception ex) when (ex is ArgumentException or UnauthorizedAccessException) { return Fail("Sipariş tarifi, tarih aralığı veya yetki geçerli değil."); }
        }
        if (recipe is null || recipe.Version != ReportDictionary.Version
            || recipe.Subject != ReportDictionary.Subject)
            return Fail("Rapor konusu veya tarif sürümü desteklenmiyor.");

        var fields = ReportDictionary.ForPermissions(permissions, attributes).ToDictionary(x => x.Id);
        if (!ValidFields(recipe.Metrics, "metric", 1, 3, fields)
            || !ValidFields(recipe.Dimensions, "dimension", 0, 3, fields))
            return Fail("Rapor alanı desteklenmiyor veya yetkiniz yok.");
        if (recipe.Limit is < 1 or > MaxRows)
            return Fail("Rapor satır sınırı 1–1000 arasında olmalı.");
        if (recipe.Presentation is not ("table" or "bar")
            || (recipe.Presentation == "bar" && recipe.Dimensions!.Length != 1))
            return Fail("Gösterim desteklenmiyor; sütun grafiği tek gruplama alanı gerektirir.");
        if (recipe.Filters is null || recipe.Filters.Length > 8)
            return Fail("Filtre listesi eksik veya çok uzun.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var filter in recipe.Filters)
        {
            if (filter?.Field is null || !fields.TryGetValue(filter.Field, out var field)
                || !seen.Add(filter.Field) || filter.Operator is null
                || !field.Operators.Contains(filter.Operator)
                || filter.Values is null || filter.Values.Length is < 1 or > 20
                || (filter.Operator == "eq" && filter.Values.Length != 1))
                return Fail("Filtre desteklenmiyor veya yetkiniz yok.");
            if (filter.Values.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 128 || x.Any(char.IsControl))
                || filter.Values.Distinct(StringComparer.Ordinal).Count() != filter.Values.Length)
                return Fail("Filtre değerleri geçerli değil.");
            if (field.Id == "warehouseId" && filter.Values.Any(x => !Guid.TryParse(x, out var id) || id == Guid.Empty))
                return Fail("Depo kimliği geçerli değil.");
            if (field.Id == "stockType" && filter.Values.Any(x => x is not ("physical" or "virtual")))
                return Fail("Stok türü fiziksel veya sanal olmalı.");
        }
        // Returned literals are data, never safe SQL fragments. Executor must parameterize them.
        return new(recipe, null);
    }

    private static bool ValidFields(string[]? ids, string kind, int min, int max,
        IReadOnlyDictionary<string, ReportField> fields) =>
        ids is not null && ids.Length >= min && ids.Length <= max
        && ids.Distinct(StringComparer.Ordinal).Count() == ids.Length
        && ids.All(x => x is not null && fields.TryGetValue(x, out var field) && field.Kind == kind);

    internal static bool HasDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
                if (!names.Add(property.Name) || HasDuplicateProperties(property.Value)) return true;
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray())
                if (HasDuplicateProperties(item)) return true;
        return false;
    }

    private static ReportValidationResult Fail(string message) => new(null, message);
}
