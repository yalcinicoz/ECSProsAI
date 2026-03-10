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
    bool IsActive);
