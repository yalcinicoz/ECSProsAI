namespace ECSPros.Shared.Contracts;

public interface IMemberService
{
    Task<MemberInfo?> GetMemberAsync(Guid memberId, CancellationToken ct = default);
    Task<bool> MemberExistsAsync(Guid memberId, CancellationToken ct = default);
}

public record MemberInfo(
    Guid MemberId,
    string FullName,
    string? Email,
    string? Phone,
    Guid? MemberGroupId,
    bool IsActive,
    string? Gender = null,               // G9: segment — yalnız profilden, tahmin yok (spec)
    Guid? CityId = null,                 // G9: profil şehri (konum zinciri 2. halka)
    Guid? DefaultAddressCityId = null);  // G9: varsayılan teslimat adresi şehri (1. halka)
