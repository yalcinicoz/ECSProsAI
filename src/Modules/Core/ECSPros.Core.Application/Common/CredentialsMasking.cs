using System.Text.Json;

namespace ECSPros.Core.Application.Common;

/// <summary>
/// Kimlik bilgisi değerleri admin'e geri inmez: GET yanıtında her değer MaskedValue ile
/// değiştirilir (anahtarlar görünür, değerler görünmez); güncellemede MaskedValue gelen
/// anahtar "değiştirilmedi" sayılır ve saklı değeri korunur. Yeni/değişen değerler
/// olduğu gibi yazılır, istekte olmayan anahtarlar silinmiş sayılır.
/// </summary>
public static class CredentialsMasking
{
    public const string MaskedValue = "•••";

    public static Dictionary<string, object> Mask(Dictionary<string, object> credentials) =>
        credentials.ToDictionary(kv => kv.Key, _ => (object)MaskedValue);

    public static Dictionary<string, object> MergeMasked(
        Dictionary<string, object> incoming, Dictionary<string, object> existing)
    {
        var result = new Dictionary<string, object>();
        foreach (var (key, value) in incoming)
        {
            if (IsMasked(value))
            {
                if (existing.TryGetValue(key, out var saved))
                    result[key] = saved;
                // maskeli ama saklıda karşılığı yok → gerçek değer hiç girilmemiş; atlanır
            }
            else
            {
                result[key] = value;
            }
        }
        return result;
    }

    private static bool IsMasked(object? value) => value switch
    {
        string s => s == MaskedValue,
        JsonElement je => je.ValueKind == JsonValueKind.String && je.GetString() == MaskedValue,
        _ => false
    };
}
