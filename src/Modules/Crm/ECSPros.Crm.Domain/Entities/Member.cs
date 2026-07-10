using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

public class Member : BaseEntity
{
    public Guid MemberGroupId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? PasswordHash { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Gender { get; set; }
    public DateOnly? BirthDate { get; set; }
    // E2: yaşadığı şehir (crm_cities) — kişiselleştirme segmenti (G9 il→bölge) buradan beslenir.
    public Guid? CityId { get; set; }
    // C7 (K9): TCKN — format+algoritma doğrulamasından geçmiş kimlik no (NVİ/KPS ileride).
    // Eşik üzeri siparişlerde zorunlu; yalnızca sipariş doğrulama süreci için kullanılır.
    public string? IdentityNumber { get; set; }
    public DateTime? IdentityVerifiedAt { get; set; }

    // D3: kayıtta onaylanan belgelerin kaydı (üyelik sözleşmesi, KVKK aydınlatma…) —
    // jsonb; "acceptedContracts" anahtarında code/title/acceptedAt/contentUpdatedAt listesi.
    public Dictionary<string, object>? Consents { get; set; }

    public string? TaxOffice { get; set; }
    public string? TaxNumber { get; set; }
    public string? CompanyName { get; set; }
    public bool IsRegistered { get; set; } = false;
    public bool IsEmailVerified { get; set; } = false;
    public bool IsPhoneVerified { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public DateTime? AnonymizedAt { get; set; }

    public MemberGroup MemberGroup { get; set; } = null!;
    public ICollection<Address> Addresses { get; set; } = new List<Address>();
    public ICollection<Cart> Carts { get; set; } = new List<Cart>();
    public Wallet? Wallet { get; set; }
    public LoyaltyAccount? LoyaltyAccount { get; set; }
    public MemberCredit? Credit { get; set; }
    public ICollection<MemberPrice> Prices { get; set; } = new List<MemberPrice>();
    public ICollection<MemberDiscount> Discounts { get; set; } = new List<MemberDiscount>();
    public ICollection<OrderTemplate> OrderTemplates { get; set; } = new List<OrderTemplate>();
}
