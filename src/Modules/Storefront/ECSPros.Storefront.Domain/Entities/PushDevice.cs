using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// Mobil push bildirim cihaz kaydı (2026-09-05): uygulama, FCM/APNs token'ını kanal
/// bazında kaydeder. MemberId null = henüz giriş yapmamış (anonim) cihaz; girişten
/// sonra aynı token yeniden gönderilince üyeye bağlanır, çıkış sonrası gönderim
/// bağlantıyı koparır. Token değişiminde (rotasyon) aynı DeviceIdentifier'ın eski
/// kayıtları revoked'a çekilir. Bildirim GÖNDERİMİ ayrı iş — bu tablo adres defteridir.
/// </summary>
public class PushDevice : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public Guid? MemberId { get; set; }
    public string Platform { get; set; } = "android"; // android | ios
    public string Token { get; set; } = string.Empty; // FCM/APNs push token
    public string? DeviceIdentifier { get; set; }     // uygulama kurulum/cihaz kimliği (rotasyon eşleşmesi)
    public string? AppVersion { get; set; }
    public string Status { get; set; } = "active";    // active | revoked
    public DateTime LastSeenAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
