using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

/// <summary>
/// Konu başlığı (eski cm_crm_konu_basliklari + _alanlar). Tür konudan türer (eski akış: formda tür seçilmez).
/// RequiredFields: konuya göre zorunlu alanlar — orderNumber | callerName | callerPhone | body | image
/// (image = en az bir ek; görsel artık gövdeye gömülmez, K5).
/// </summary>
public class TicketSubject : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "request";        // complaint | request  (eski TurID 1 | 2)
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public List<string> RequiredFields { get; set; } = new();   // jsonb
    public int? LegacyId { get; set; }                   // cm_crm_konu_basliklari.KonuBasligiID
}
