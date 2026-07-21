using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Iam.Domain.Entities;

/// <summary>
/// definition.api_client_types — platformca tanımlı API kullanıcı tipi kataloğu.
/// Her tip sabit (kilitli) bir taban scope paketi taşır; hesap yaratılırken scope tek tek
/// seçilmez, tipten türetilir. definition altın kuralı geçerli: yalnız geliştirici firma
/// doldurur, veri aktarımları/eşlemeler kayıt EKLEYEMEZ (bkz. docs/api-hesaplari-tasarimi.md §3).
/// </summary>
public class ApiClientType : BaseEntity
{
    /// <summary>supplier_managed | supplier_merchant | first_party | internal</summary>
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();

    /// <summary>Güven ekseni varsayılanı (ApiClient.ClientType): internal | first_party | partner</summary>
    public string DefaultClientType { get; set; } = "partner";

    /// <summary>"current_account" → hesap sahipsiz açılamaz; null → sahip zorunlu değil.</summary>
    public string? RequiredOwnerType { get; set; }

    /// <summary>Kilitli taban scope paketi (§3). Gönderim bayrağı supplier ise etkin scope'a
    /// order.read + fulfillment.write eklenir (ApiScopes.SupplierFulfillment).</summary>
    public List<string> BaseScopes { get; set; } = new();

    public bool IsActive { get; set; } = true;
}
