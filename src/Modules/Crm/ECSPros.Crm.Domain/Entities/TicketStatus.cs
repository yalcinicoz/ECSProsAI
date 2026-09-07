using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

/// <summary>
/// Müşteri İlişkileri kayıt durumu (eski cm_crm_durum; 2026-09-07 K6). Firma verisidir, panelden yönetilir.
/// IsHidden: varsayılan listede görünmez, yeni işlemde seçilemez, filtreyle görünür ("Çözülemedi").
/// ExemptFromDuplicateCheck: bu durumdaki kayıt mükerrer kontrolünde sayılmaz ("Hatalı Kayıt").
/// </summary>
public class TicketStatus : BaseEntity
{
    public string Code { get; set; } = string.Empty;     // beklemede | ilgili_birimde | cozulemedi | cozuldu | hatali_kayit | tazmin_surecinde
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#6b7280";       // liste satırı / rozet rengi
    public int SortOrder { get; set; }
    public bool IsHidden { get; set; }
    public bool IsResolved { get; set; }                 // "Çözüldü" — yeşil, kapanış anlamı
    public bool ExemptFromDuplicateCheck { get; set; }
    public bool IsDefault { get; set; }                  // yeni kayıt bu durumla açılır ("Beklemede")
    public int? LegacyId { get; set; }                   // cm_crm_durum.DurumID
}
