using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Crm.Application.Queries.GetMemberDetail;

public record GetMemberDetailQuery(Guid MemberId) : IRequest<Result<MemberDetailDto>>;

public record MemberDetailDto(
    Guid Id,
    Guid MemberGroupId,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? Gender,
    DateOnly? BirthDate,
    string? TaxOffice,
    string? TaxNumber,
    string? CompanyName,
    bool IsRegistered,
    bool IsEmailVerified,
    bool IsPhoneVerified,
    bool IsActive,
    DateTime? LastLoginAt,
    DateTime CreatedAt);
