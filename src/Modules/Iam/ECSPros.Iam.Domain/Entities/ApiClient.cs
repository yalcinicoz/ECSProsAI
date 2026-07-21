using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Iam.Domain.Entities;

/// <summary>
/// iam.api_clients — site üyesinden (Member) ve personelden (User) BAĞIMSIZ üçüncü kimlik türü
/// (makine kimliği). client_credentials akışıyla token alır (type=api_client). Scope'lar hesapta
/// serbest tutulmaz; ApiClientTypeCode (+ FulfillmentMode) üzerinden token üretiminde çözülür.
/// </summary>
public class ApiClient : BaseEntity
{
    public string Name { get; set; } = string.Empty;                 // "Acme Tedarik Entegrasyonu"
    public string ClientId { get; set; } = string.Empty;             // ecs_live_...  public, unique
    public string SecretHash { get; set; } = string.Empty;           // BCrypt — düz secret ASLA saklanmaz
    public string SecretHint { get; set; } = string.Empty;           // "••••4f2c" — panelde gösterim

    /// <summary>Güven ekseni: internal | first_party | partner</summary>
    public string ClientType { get; set; } = "partner";
    /// <summary>İş rolü — definition.api_client_types kataloğuna referans (kod).</summary>
    public string ApiClientTypeCode { get; set; } = string.Empty;
    public string? OwnerType { get; set; }                           // current_account
    public Guid? OwnerId { get; set; }                               // accounts.current_accounts.Id

    /// <summary>Gönderim modeli (yalnız tedarikçi tiplerinde anlamlı): platform | supplier.
    /// supplier ise etkin scope'a order.read + fulfillment.write eklenir (Yol B bayrağı).</summary>
    public string FulfillmentMode { get; set; } = "platform";

    public List<string> IpAllowList { get; set; } = new();           // boşsa kısıt yok
    public int RateLimitPerMinute { get; set; } = 120;

    public bool IsActive { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public string? LastUsedIp { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? RevokedBy { get; set; }
}
