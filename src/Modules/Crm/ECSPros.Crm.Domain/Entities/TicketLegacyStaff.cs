using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

/// <summary>Eski panel personeli (dfpersonel) → IAM kullanıcısı eşlemesi; aktarım tekrar çalıştırılabilir olsun diye kalıcı (K3).</summary>
public class TicketLegacyStaff : BaseEntity
{
    public int LegacyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Username { get; set; }
    public Guid? UserId { get; set; }
}
