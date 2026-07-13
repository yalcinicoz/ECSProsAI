using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ECSPros.Core.Infrastructure.Persistence;

/// <summary>
/// FirmPlatformIntegration.Credentials sözlüğünü tek text kolonunda Data Protection ile
/// at-rest şifreli saklar (DB dump'ı sızsa da API key/şifreler okunamaz). Key ring
/// Program.cs'de kalıcı dizine bağlanır — key ring kaybolursa kayıtlar çözülemez,
/// kimlik bilgilerinin yeniden girilmesi gerekir. Okumada düz JSON'a ('{' ile başlayan
/// değer) tolerans vardır: şifreleme öncesi/elle yazılmış kayıt da çözülür.
/// Protector null ise (DI dışı test host'u) düz JSON yazılır.
/// </summary>
public static class EncryptedCredentials
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public static ValueConverter<Dictionary<string, object>, string> CreateConverter(IDataProtector? protector) =>
        new(v => Encrypt(v, protector), s => Decrypt(s, protector));

    // Değer nesnesi değiştirilebilir (mutable dictionary) — EF'in değişikliği içerikten
    // algılaması için JSON temelli karşılaştırıcı şart (referans karşılaştırması yetmez).
    public static ValueComparer<Dictionary<string, object>> Comparer { get; } = new(
        (a, b) => JsonSerializer.Serialize(a, JsonOpts) == JsonSerializer.Serialize(b, JsonOpts),
        v => JsonSerializer.Serialize(v, JsonOpts).GetHashCode(),
        v => JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(v, JsonOpts), JsonOpts)!);

    private static string Encrypt(Dictionary<string, object> value, IDataProtector? protector)
    {
        var json = JsonSerializer.Serialize(value, JsonOpts);
        return protector is null ? json : protector.Protect(json);
    }

    private static Dictionary<string, object> Decrypt(string stored, IDataProtector? protector)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return new Dictionary<string, object>();

        var json = protector is null || stored.TrimStart().StartsWith('{')
            ? stored
            : protector.Unprotect(stored);
        return JsonSerializer.Deserialize<Dictionary<string, object>>(json, JsonOpts)
               ?? new Dictionary<string, object>();
    }
}
