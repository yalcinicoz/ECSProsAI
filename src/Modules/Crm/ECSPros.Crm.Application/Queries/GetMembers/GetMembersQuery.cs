using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Crm.Application.Queries.GetMembers;

public record GetMembersQuery(
    string? Search = null,
    bool ActiveOnly = true,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedMemberResult>>;

public record PagedMemberResult(List<MemberListDto> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public record MemberListDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    bool IsRegistered,
    bool IsActive,
    DateTime CreatedAt);
