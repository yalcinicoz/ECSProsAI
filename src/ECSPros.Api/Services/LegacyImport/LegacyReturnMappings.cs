namespace ECSPros.Api.Services.LegacyImport;

public static class LegacyReturnMappings
{
    public static string ReasonCode(int sourceReasonId) => sourceReasonId switch
    {
        1 => "legacy_unspecified",
        2 => "legacy_disliked",
        3 => "legacy_size",
        9 => "legacy_not_delivered",
        _ => "legacy_unknown"
    };

    // Kaynak 1/2 iş anlamı operasyon tarafından henüz doğrulanmadı; tahmin edilmeden korunur.
    public static string ReturnType(int rawType) => $"legacy_type_{rawType}";
    public static string RefundMethod(int rawMethod) => $"legacy_type_{rawMethod}";
}
