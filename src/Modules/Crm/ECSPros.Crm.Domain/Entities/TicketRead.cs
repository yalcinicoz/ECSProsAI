using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

/// <summary>Okundu kaydı (eski cm_crm_kontrol_edenler): kullanıcı kaydı açtığında o ana kadarki her işlem için bir satır.</summary>
public class TicketRead : BaseEntity
{
    public Guid TicketId { get; set; }
    public Guid ActivityId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int? LegacyId { get; set; }
}
