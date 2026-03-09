using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Crm.Application.Commands.CreateMember;

public record CreateMemberCommand(
    Guid MemberGroupId,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? Password,
    string? Gender,
    DateOnly? BirthDate,
    string? CompanyName,
    string? TaxNumber,
    string? TaxOffice) : IRequest<Result<Guid>>;
