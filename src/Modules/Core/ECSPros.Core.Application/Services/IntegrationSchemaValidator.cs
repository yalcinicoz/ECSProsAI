using System.Text.Json;
using ECSPros.Core.Domain.Entities;

namespace ECSPros.Core.Application.Services;

/// <summary>
/// Firma entegrasyon kaydında servis şemasının `Required` alanlarını doğrular (2026-08-22 — kullanıcı
/// testinde Meta/Merchant kaydı pixelId/merchantId olmadan kaydedilebildi). Credentials'ta maskeli
/// değer ("•••") saklı değerin korunması anlamına gelir → dolu sayılır. Şema yoksa/bozuksa doğrulama yapılmaz.
/// </summary>
public static class IntegrationSchemaValidator
{
    private static readonly JsonSerializerOptions Ayar = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Eksik zorunlu alan etiketleri (tr) — boşsa geçerli.</summary>
    public static List<string> EksikZorunlular(string? settingsSchemaJson,
        IReadOnlyDictionary<string, object> credentials, IReadOnlyDictionary<string, object> settings)
    {
        var eksik = new List<string>();
        if (string.IsNullOrWhiteSpace(settingsSchemaJson)) return eksik;
        List<PlatformSchemaField> sema;
        try { sema = JsonSerializer.Deserialize<List<PlatformSchemaField>>(settingsSchemaJson, Ayar) ?? new(); }
        catch { return eksik; }

        foreach (var alan in sema.Where(f => f.Required))
        {
            var kaynak = string.Equals(alan.Section, "credentials", StringComparison.OrdinalIgnoreCase) ? credentials : settings;
            if (kaynak.TryGetValue(alan.Key, out var v) && Dolu(v)) continue;
            // bölüm karışmış olabilir (eski kayıtlar) — diğer bölümde doluysa kabul
            var diger = ReferenceEquals(kaynak, credentials) ? settings : credentials;
            if (diger.TryGetValue(alan.Key, out var v2) && Dolu(v2)) continue;
            eksik.Add(alan.LabelI18n.TryGetValue("tr", out var l) && !string.IsNullOrWhiteSpace(l) ? l : alan.Key);
        }
        return eksik;
    }

    private static bool Dolu(object? v) => v switch
    {
        null => false,
        string s => !string.IsNullOrWhiteSpace(s),
        bool => true,
        JsonElement je => je.ValueKind switch
        {
            JsonValueKind.String => !string.IsNullOrWhiteSpace(je.GetString()),
            JsonValueKind.Null or JsonValueKind.Undefined => false,
            _ => true
        },
        _ => !string.IsNullOrWhiteSpace(v.ToString())
    };
}
