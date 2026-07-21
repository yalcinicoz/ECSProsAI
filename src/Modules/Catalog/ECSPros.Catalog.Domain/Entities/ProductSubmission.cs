using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

/// <summary>
/// catalog.product_submissions — Partner API'den (façade) gelen ürün kartı gönderimi (staging).
/// Façade canlı katalogu DEĞİL bunu yazar; Kapı 1 (otomatik doğrulama) geçen gönderim `pending`
/// olur, Kapı 2'de (insan onayı) kabul edilip canlı Product'a dönüşür veya reddedilir
/// (docs/api-hesaplari-tasarimi.md §3.6/§3.8). Owner = SupplierId (= ApiClient.OwnerId).
/// </summary>
public class ProductSubmission : BaseEntity
{
    public Guid SupplierId { get; set; }                          // sahip (accounts.current_accounts.Id)
    public string SupplierProductCode { get; set; } = string.Empty; // externalCode — (SupplierId, kod) upsert
    public string GroupCode { get; set; } = string.Empty;
    public Dictionary<string, string> Name { get; set; } = new(); // liste/panel için snapshot
    public string PayloadJson { get; set; } = "{}";               // ham gönderim (jsonb) — onayda kullanılır
    public int VariantCount { get; set; }

    public string Status { get; set; } = "pending";              // pending | approved | rejected
    public Guid? ApiClientId { get; set; }                        // gönderen makine kimliği

    public string? ProductCode { get; set; }                     // onaylanınca atanır
    public Guid? ProductId { get; set; }
    public string? ReviewNote { get; set; }                      // kabul/red notu (Kapı 2)
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
}
