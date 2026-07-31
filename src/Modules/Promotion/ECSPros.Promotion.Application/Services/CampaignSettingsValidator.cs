using System.Text.Json;
using ECSPros.Promotion.Domain.Entities;

namespace ECSPros.Promotion.Application.Services;

/// <summary>
/// F1: platformun doldurduğu Settings'i tipin SettingsSchema şablonuna göre doğrular
/// (zorunlu alanlar dolu mu, sayısal alanlar sayı mı). Koşullu (visibleWhen) alanlar yalnız
/// koşul sağlanınca zorunludur.
/// </summary>
public static class CampaignSettingsValidator
{
    public static string? Validate(List<CampaignSchemaField>? schema, Dictionary<string, object> settings)
    {
        if (schema is null || schema.Count == 0) return null;

        foreach (var f in schema)
        {
            // Koşullu görünürlük: koşul sağlanmıyorsa alan zorunlu değil.
            if (f.VisibleWhen is { } cond && !ConditionMet(cond, settings))
                continue;

            var has = settings.TryGetValue(f.Key, out var val) && !IsEmpty(val);

            if (f.Required && !has)
                return $"'{Label(f)}' alanı zorunlu.";

            if (has && f.Type is "number" or "integer" or "percent" or "money" && !IsNumeric(val!))
                return $"'{Label(f)}' sayısal olmalı.";
        }
        return null;
    }

    private static bool ConditionMet(CampaignSchemaFieldCondition cond, Dictionary<string, object> settings)
    {
        var cur = settings.TryGetValue(cond.Field, out var v) ? AsString(v) : null;
        if (cond.EqualsValue is not null) return cur == cond.EqualsValue;
        if (cond.NotEqualsValue is not null) return cur != cond.NotEqualsValue;
        return true;
    }

    private static string Label(CampaignSchemaField f) =>
        f.LabelI18n.TryGetValue("tr", out var tr) ? tr : f.Key;

    private static bool IsEmpty(object? val) => val switch
    {
        null => true,
        string s => string.IsNullOrWhiteSpace(s),
        JsonElement je => je.ValueKind == JsonValueKind.Null ||
                          (je.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(je.GetString())),
        _ => false
    };

    private static bool IsNumeric(object val) => val switch
    {
        int or long or double or decimal => true,
        string s => decimal.TryParse(s, out _),
        JsonElement je => je.ValueKind == JsonValueKind.Number ||
                          (je.ValueKind == JsonValueKind.String && decimal.TryParse(je.GetString(), out _)),
        _ => false
    };

    private static string? AsString(object? val) => val switch
    {
        null => null,
        string s => s,
        JsonElement je => je.ValueKind == JsonValueKind.String ? je.GetString() : je.ToString(),
        _ => val.ToString()
    };
}
