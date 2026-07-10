using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

/// <summary>D4: SMS ile gönderilen tek kullanımlık doğrulama kodu.
/// Kod düz metin saklanmaz (SHA256 hex); süre + deneme sınırı handler'da uygulanır.</summary>
public class OtpCode : BaseEntity
{
    /// <summary>Normalize telefon — ülke koduyla, yalnız rakam (örn. 905551112233).</summary>
    public string Phone { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    /// <summary>Kodun amacı (login; ileride iade doğrulama E8 vb.).</summary>
    public string Purpose { get; set; } = "login";
    public DateTime ExpiresAt { get; set; }
    public int AttemptCount { get; set; }
    /// <summary>Başarıyla kullanıldığı ya da yenisiyle geçersiz kılındığı an.</summary>
    public DateTime? ConsumedAt { get; set; }
}
