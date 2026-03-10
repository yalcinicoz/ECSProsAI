using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Crm.Application.Commands.UpdateMember;

public record UpdateMemberCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? Gender,
    DateOnly? BirthDate,
    string? TaxOffice,
    string? TaxNumber,
    string? CompanyName,
    bool IsActive,
    Guid? MemberGroupId,
    Guid UpdatedBy) : IRequest<Result>;
