using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Iam.Domain.Entities;

/// <summary>
/// Yetki grubunun (panelde "Yetki Grubu", DB'de Role) bir permission'ı VERMESİ.
/// Gruplar yalnız verir; yasaklama yalnız kullanıcı istisnasındadır (tasarım Ek-1).
/// </summary>
public class RolePermission : BaseEntity
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }

    /// <summary>Kanal kapsamlı permission'da bu grubun kattığı kanallar (kanal id listesi).
    /// null/boş = kanal yok. Kanal kapsamsız permission'da YOK SAYILIR.
    /// "Tüm kanallar" seçimi kayıt anındaki kanalların listesi olarak yazılır (Ek-2) —
    /// gelecekte açılan kanal otomatik kapsanmaz.</summary>
    public List<Guid>? ChannelIds { get; set; }

    public Role Role { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}
