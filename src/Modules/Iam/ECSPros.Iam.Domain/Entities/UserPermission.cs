using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Iam.Domain.Entities;

/// <summary>
/// Kullanıcıya özel istisna (tasarım Ek-1): gruplardan bağımsız VERME ya da gruptan geleni
/// KALDIRMA. Kullanıcı kararı grup sonucundan güçlüdür; hesapta en son uygulanır.
/// Panelde ALLOW/DENY/INHERIT gibi teknik kavram gösterilmez — "bu kullanıcıda aç/kapat".
/// </summary>
public class UserPermission : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid PermissionId { get; set; }

    /// <summary>grant = kullanıcıya özel ver · revoke = kullanıcıda kaldır.</summary>
    public string GrantType { get; set; } = "grant";

    /// <summary>Kanal kapsamlı permission'da istisnanın geçerli olduğu kanallar.
    /// "Tümünde kaldır/ver" seçimi kayıt anındaki kanalların listesi olarak yazılır (Ek-2).
    /// Kanal kapsamsız permission'da yok sayılır (istisna doğrudan var/yok anlamındadır).</summary>
    public List<Guid>? ChannelIds { get; set; }

    /// <summary>KULLANILMIYOR (2026-09-09): kapsam birimi kanaldır (K1); firma kanaldan türetilir.
    /// Kolon geriye dönük uyumluluk için duruyor, hesaba GİRMEZ.</summary>
    public Guid? FirmId { get; set; }

    public User User { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}
